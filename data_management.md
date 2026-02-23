# Data Management Workflow (CSV → SQLite)

This document describes the data management workflow for the autobattler project, supporting both rapid development and robust production builds.

## Overview

The engine loads registry data (abilities, archetypes, conditions, status effects) from either CSV files or a SQLite database. This enables fast iteration during development and efficient, reliable data access in production.

## Steps

### 1. CSV-Based Development Workflow
- Define CSV formats for each registry (abilities, archetypes, conditions, status effects).
- Implement a CSV loader utility in Core/Data (pure C#).
- Populate registries from parsed CSV data for fast iteration.

### 2. SQLite Integration for Production
- Design SQLite schema matching your data models (Ability, Archetype, etc.).
- Implement a database loader utility in Core/Data using a portable C# SQLite library (e.g., System.Data.SQLite or Microsoft.Data.Sqlite).
- Populate registries from database queries.
- Provide a migration tool or script to convert CSVs to SQLite (one-time or automated).

### 3. Configurable Data Source
- Add a configuration option (e.g., environment variable, build flag, or settings file) to select data source: CSV or SQLite.
- On startup, load from CSVs if in development mode; load from SQLite if in production.

### 4. Validation & Error Handling
- Validate data on load (from either source).
- Log or report errors for malformed CSVs or database issues.

### 5. Shipping SQLite with Game
- Package the SQLite database file with the game build.
- Ensure read-only access and integrity checks.

## Verification
- Test registry loading from CSVs and SQLite.
- Confirm migration tool converts CSVs to SQLite correctly.
- Validate game build loads data from SQLite and functions as expected.

## Decisions
- CSV for rapid, human-editable development.
- SQLite for efficient, robust production data.
- Configurable source for seamless workflow.

---

For implementation details, see the Core/Data loader utilities and migration scripts.