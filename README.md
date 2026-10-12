# Sensor Ingestion, Stateful Rule Engine & Alerting

My submission for the i4Twins backend-engineer task. The service reads `readings.jsonl`, cleans out
duplicates and invalid records, evaluates every remaining reading against rules loaded from
`rules.json`, including the stateful `SustainedAbove` operator, raises alerts for sustained
violations, stores the results in SQLite so that processing the same file twice is safe, and serves
time-bucketed aggregation of the acceptable data over HTTP.


## Architecture

The solution has four projects with dependencies pointing inward:

- **I4Twins.Domain** - readings, rule definitions, operators, episode detection, classification. Plain
  C#, no I/O and no framework types.
- **I4Twins.Application** - the pipeline: ingestion orchestration, rule evaluation, alert decisions,
  aggregation, the processing report. Declares the interfaces the outside world must fulfill
  (line/rule sources and parsers, a processing store, an acceptable-reading source, a log sink).
- **I4Twins.Infrastructure** - implements the interfaces: JSON parsing, file
  access, SQLite via raw ADO.NET, console logging.
- **I4Twins.Api** - a thin ASP.NET Core host that wires everything together at startup and exposes
  one endpoint.

The payoff is testability and honest separation: validation, deduplication, applicability, episode
logic, cooldown and bucketing are all plain domain/application code that the unit tests exercise with
no file system or database, while the integration tests run the real files, the real database and the
real HTTP endpoint through a test server.

## Ingestion and dirty data

A reading is identified by `(deviceId, metric, ts, seq)`. Two records with equal quadruples are
duplicates even if their values differ; the same `ts` with a different `seq` is a different reading.

For conflicting duplicates I chose **first valid occurrence wins**. Records are processed in the order parse → validate → deduplicate: a
record must be fully valid before it can even be a duplicate candidate, and the invalid ones are
counted as invalid, never as duplicates.

Validation rejects missing or empty `deviceId`/`metric`, timestamps that are unparseable or not an explicit UTC instant, non-numeric or non-finite values, and non-integer sequence numbers. A malformed
or invalid line never stops the run - it is counted, logged with its line number, and processing
continues. Extreme but finite values are kept as valid readings;

A rule violation is not dirty data. Invalid and duplicate readings never reach evaluation and are
never counted as unacceptable. the report keeps those categories apart.

## Rules and evaluation

The task ships no `rules.json`, so I authored a small seed file with the interesting paths:
a global `GreaterThan` on temperature, a device-specific `SustainedAbove` on PUMP-01, and a global
`Between` on vibration. Thresholds were picked from the real data ranges so the rules actually fire.
Each rule has `id`, `name`, `metric`, optional `deviceId` (omit or null = global), `operator`,
`enabled`, plus the operator's parameters (`threshold`, `min`/`max`, `durationSeconds`). Enabling,
disabling or retuning a rule is a file edit, not a code change.

The operators are **violation conditions**: `GreaterThan 80` means a temperature of 90 makes the
reading unacceptable. I read the task the same way for the stateful operator (a sustained breach
violates), so the semantics are consistent across all operators. A reading that violates several
applicable rules is reported with **all** of them - rule id and a reason each - and a reading no
enabled rule applies to is acceptable.

`Between` is an inclusive window `[min, max]` and only values outside it violate; `Equal` is exact numeric equality (float noise can miss - a documented
limitation); operator names match case-sensitively; `durationSeconds` must be positive; a rule
without an `enabled` flag is disabled; a `deviceId` present but empty is a configuration error.

Configuration fails loudly. The loader collects every problem - unknown operator, missing parameter,
duplicate id, wrong type - and reports them all with the offending rule's position and id rather than
starting up with a partial rule set.

Extensibility was a stated requirement, so operators are plugins: each implements `IRuleOperator`
(its name plus its own parameter validation) and optionally `IStatelessRuleOperator` (a per-reading
predicate) or `IStatefulRuleOperator` (temporal, over an ordered stream), then registers in a
registry. Adding an operator is one class plus one registration line; nothing in the evaluation code
changes, and a test proves it by running a brand-new operator through the engine.

## SustainedAbove: state, ordering, and the questions the task asks about

`SustainedAbove` is violated when a metric stays above a threshold for at least `durationSeconds` of
**event time**. The file is not time-sorted, so ordering had to be a deliberate choice.

I chose the **batch** strategy the task allows: sort all valid, deduplicated readings by event time,
with `seq` as the tie-breaker for equal timestamps, then evaluate. For a bounded file of ~2,000
readings this is far simpler and more testable than streaming state with watermarks, and it makes
out-of-order input a non-issue by construction - the same logical stream in any file order produces
identical episodes and classifications, which the tests check. A streaming implementation would trade
that simplicity for incremental state and late-data handling that this input doesn't need.

The episode itself is a scan over one `(ruleId, deviceId, metric)` stream - state never shared
between devices or rules:

- an episode **starts** at the first reading above the threshold and **ends** at the first reading at
  or below it;
- between samples the value is assumed to persist. That is the continuity assumption: the dataset
  gives discrete samples, and I preferred stating the assumption over inventing a maximum-gap policy
  the task doesn't define. For much sparser real streams this is the first thing I would revisit;
- an episode **qualifies** when `endTs − startTs ≥ durationSeconds`; if the input ends while an
  episode is still active, its end is the last above-threshold reading and the same test applies;
- classification is **retroactive**: once an episode qualifies, every above-threshold reading in it
  is unacceptable - at the time those readings arrived the violation wasn't knowable yet, but batch
  evaluation knows the future, and marking them is the honest result. The terminating at-or-below
  reading stays acceptable; a non-qualifying episode marks nothing.

## Alerts and cooldown

Episode detection and the decision to alert are separate steps. One qualifying episode produces at
most one alert - never one per offending reading - carrying `ruleId`, `deviceId`, `metric`,
`startTs`, `endTs` and the peak value.

Cooldown is the task's 5-minute default per `(ruleId, deviceId, metric)`. The subtle part is which
timestamp drives it; I measure **episode separation**: a new episode raises an alert only if its
start is strictly more than five minutes after the previous alert's end, which matches the task's
"episodes separated by more than the cooldown produce separate alerts". The anchor is the last
*raised* alert's end - a suppressed episode doesn't push it forward. Suppressed episodes are not
discarded: every detected episode is tracked with a raised/suppressed flag, and the report shows both.

## Storage and idempotent re-processing

I chose SQLite (raw ADO.NET, no ORM). The main reason is the idempotency requirement: in-memory
storage can only prove "no duplicates within one process", while SQLite proves it across real
restarts, and the unique constraints enforce the guarantee at the storage boundary rather than
trusting application code.

Each output has a stable identity that doubles as its uniqueness constraint:

| Table | Key |
|-------|-----|
| `readings` | `(device_id, metric, ts, seq)` |
| `rule_results` | reading identity + `rule_id` |
| `episodes` / `alerts` | `(rule_id, device_id, metric, start_ts)` |

All inserts are `INSERT OR IGNORE`, and one transaction wraps the whole batch, so a run either
persists completely or not at all. `rule_results` stores every evaluated (reading, rule) pair -
violated or not - which makes "rule evaluations performed" reconstructible from the database, not
just from memory. Timestamps are stored as ISO round-trip UTC text so the key columns are stable
across runs.

Re-running the same input inserts nothing new. This is demonstrated by integration tests that
process the real dataset in two separate executions against one database and verify zero new rows
and unchanged state after the second run.

## Processing report

The report at the end of a run prints every counter the task lists. Two distinctions are worth
calling out because they are easy to get wrong. First, one unacceptable reading that violates three
rules contributes **one** to unacceptable readings and **three** to rule violations (and three to
evaluations) - there is a test pinning exactly that. Second, "rows inserted this run" is separate
from totals: on an idempotent re-run the report shows the same ingestion and evaluation counters
(they are properties of the input) but zero new rows.

Logging follows the same discipline: rejections are logged at warning with the line number,
sustained episodes at warning, alerts raised/suppressed at information, and nothing is logged per
normal reading.

## Aggregation API

`GET /api/aggregations` takes `deviceId`, `metric`, `from`, `to` and `bucketSeconds` and returns, for
each equally sized bucket, its start time, count, average, min and max. Only acceptable readings
contribute - a test plants an offending reading inside a bucket and verifies it is excluded from
every statistic.

The range is half-open `[from, to)`, buckets are anchored at `from` (a reading exactly on a boundary
belongs to the later bucket), and empty buckets are omitted - a range with no readings returns an
empty list. Bucket membership uses integer tick arithmetic so a reading can never drift across a
boundary through floating-point rounding. All five parameters are required; bad timestamps, a
non-positive bucket size, or a reversed/empty range return 400. The endpoint only parses and
validates parameters; the bucketing itself is application code, not SQL, which keeps it unit-testable
and keeps the anchoring rule in one place.

## Results on the supplied dataset

Measured from a real run over a clean database (the integration tests assert these as regression
anchors):

| Stage | Result |
|-------|--------|
| Ingestion | 2150 lines → 2103 stored, 38 duplicates removed, 9 invalid rejected (1 malformed, 8 invalid fields) |
| Rules | 3 loaded |
| Evaluation | 1680 evaluations, 1942 acceptable, 161 unacceptable, 161 violations |
| Episodes / alerts | 4 episodes detected, 2 qualifying, 1 alert raised, 1 suppressed by cooldown |
| Persistence | 3795 rows; second run inserts 0 |


## AI disclosure

I used AI (ChatGPT) for researching ideas and help refining the high level architecture.
I also used coding agents (specifically ZCode, GLM model) to write tests based on the provided data and debugging when tests failed. The design decisions, the interpretation of the task's ambiguous points, and the seed-rule choices above are mine.
