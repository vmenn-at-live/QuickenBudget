# Selectors and Filters

The rules for post-processing transactions come in three properties of the `TransactionSelectors` section of the config file —
`ExpensesGroupSelectors`, `IncomeGroupSelectors`, and `TransactionFilters`.

Rules are organized into named arrays (object properties). The array names can be any identifiers; they are used only to group rules and
must be distinct to avoid merging arrays from different sources. Arrays are processed in reverse order of reading (latest arrays are processed first),
so the last declared array has the highest precedence. Within each array the contained rules are processed in forward order, so the first matching rule
inside that array wins.

Both selector and filter objects describe criteria that match transactions. The `Field` property specifies a transaction field (e.g., `Category`, `Memo`).
The `Filter` property is a regular expression (applied to the trimmed field value). If the regex matches, the rule is considered matched.

Filters are evaluated first and are used to discard transactions. A filter may optionally include `AmountOperation` and `Amount` properties
to compare numeric amounts. If a filter has `AmountOperation`/`Amount` but no `Field`/`Filter`, the amount comparison alone can trigger the filter.

Amount comparison semantics:
- `AmountOperation: "Greater"` — discard when transaction amount > `Amount`.
- `AmountOperation: "Less"` — discard when transaction amount < `Amount`.
- `AmountOperation: "Equal"` — discard when transaction amount == `Amount`.
- `AmountOperation: "None"` (or omitted) — no amount check is performed; matching is based solely on `Field`/`Filter`.

If no filter discards the transaction, selector rules are evaluated to assign a `TargetGroup`. The first matched selector (after processing order)
determines the group. `ExpensesGroupSelectors` mark matched transactions as expenses; `IncomeGroupSelectors` mark them as income. Expenses selectors are tried before income selectors.

If a transaction is not discarded by any filter and is not matched by any selector, it is placed in an **"Other"** group. Its income/expense classification is then derived from the sign of the amount (positive → income, negative → expense).

Example (valid JSON) — shows filters, an expenses selector and an income selector:

```json
{
  "TransactionSelectors": {
    "ExpensesGroupSelectors": {
      "Child": [
        {"Field": "Category", "Filter": "^\\[Child Account\\]$", "TargetGroup": "Child"}
      ]
    },
    "IncomeGroupSelectors": {
      "Investment": [
        { "Field": "Category", "Filter": "^_DivInc", "TargetGroup": "Dividends" }
      ]
    },
    "TransactionFilters": {
      "ExcludeZeroAmount": [
        { "AmountOperation": "Equal", "Amount": 0 }
      ],
      "Transfers": [
        { "Field": "Category", "Filter": "^\\[(?!Child Account)[^\\]]*\\]$", "AmountOperation": "None" }
      ]
    }
  }
}
```

Note above that the filter with name `Transfers` discards almost all transactions that have a string in square brackets regardless of the amount
as `AmountOperation` is set to `None` (bracketed strings are a Quicken convention for account transfers).
The regular expression in the filter matches all account transfers except the transfers to/from the `Child Account`.

The expenses selector in the `Child` set assigns transfers to/from the `Child Account` to the `Child` expenses group.

See `QuickenBudget/config/example-selectors.json` (relative to the repository root) for a checked-in example,
and optionally create your own local-only `QuickenBudget/config/PrivateSelectors.json` (not committed) for personal rules.

