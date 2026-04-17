---
# AUTO-GENERATED from .github/instructions/genesis/sql-migrations.instructions.md — do not edit
paths:
  - "NexusAI.Bootstrap/Scripts/**/*.sql"
---
# SQL Schema Script Rules

## Purpose

`Script.0001.sql` is the **intended-state schema definition** — it describes what the database should look like, not how to get there.

## ⚠️ No Migration Logic

**SQL files must NEVER contain migration code.** No `ALTER TABLE`, no `RENAME COLUMN`, no `SET @var`, no `PREPARE`/`EXECUTE`, no conditional DDL. These files describe the target schema only.

When a schema change requires migrating existing databases, the agent must:
1. Update the `CREATE TABLE` statement to reflect the new intended state
2. Inform the user that a manual migration is needed
3. Wait for the user to ask for the migration query separately — do NOT embed it in the SQL files

## Manual Migrations

Migrations are executed **manually** against the database — there is no automated migration runner.
**Every time you modify `Script.0001.sql`, remind the user that they need to run the migration manually.**

## File Rules

- All schema changes go in `Script.0001.sql` — this is the single source of truth for the database schema
- Never create additional migration files for schema changes

## Table Creation

- Always use `CREATE TABLE IF NOT EXISTS` — never `CREATE TABLE` without the guard
- **Never** use `DROP TABLE`, `DROP DATABASE`, or any destructive DDL
- Always specify `ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci`
- Always define a `PRIMARY KEY` — no keyless tables

## Column Conventions

- Use `bigint` for IDs
- Use `varchar(N)` with an explicit max length for strings — never unbounded `text` for columns that are indexed or constrained
- Use `datetime(3)` for timestamps (millisecond precision)
- Use `json` for structured metadata columns
- Always specify `NOT NULL` explicitly unless NULL is intentionally needed — don't rely on implicit nullability
- Use `DEFAULT` values where a sensible default exists

## Indexes

- Define indexes **inline** with `CREATE TABLE`, not as separate `CREATE INDEX` statements
- Name indexes with a consistent convention: `column_name_INDEX` for single-column, `descriptive_name_CONSTRAINT` for composite/unique
- Composite indexes should list columns in selectivity order (most selective first)
- Only add indexes for columns that appear in `WHERE`, `JOIN`, or `ORDER BY` clauses — every index slows writes
- Use `UNIQUE KEY` constraints to enforce natural uniqueness
- When adding indexes to existing tables outside of `CREATE TABLE`, use `CREATE INDEX IF NOT EXISTS`

## Foreign Keys

- **Do not use explicit foreign key constraints.** Referential integrity is enforced at the application layer.

## Safety

- Never use `TRUNCATE` or `DELETE FROM` without a `WHERE` clause
- Never use `ALTER TABLE ... DROP COLUMN` in the same script that still references the column
- Never use `DROP` on any database object
