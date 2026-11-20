# 1. Record architecture decisions

Date: 2026-11-20 · Status: accepted

## Context

Qirsh is small, but it makes several choices (service split, messaging, persistence) that a reader of the code
will want explained. Decisions get lost in commit messages.

## Decision

Keep lightweight Architecture Decision Records in `docs/adr`, one file per decision, numbered, with context,
decision and consequences. Superseded records stay, marked as such.

## Consequences

New structural choices come with an ADR in the same pull request.
