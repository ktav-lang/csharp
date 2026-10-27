# Mutation microbenchmark

Run from the repository root:

```text
dotnet run -c Release --project benchmarks/Mutations --
dotnet run -c Release --project benchmarks/Mutations -- 10000 --backend=pure
dotnet run -c Release --project benchmarks/Mutations -- 10000 --backend=native
```

The iteration count is optional (default 2,000); backend defaults to `both`. `--backend=pure` does not require the native library; `--backend=native` requires it. Each selected case/backend gets 200 warmups. CSV uses invariant-culture decimal formatting and a fixed header: case, backend, iterations, elapsed milliseconds, ns/op, UTF-8 source bytes.

Cases retain the wide 64-entry insert/remove/rename baseline and cover commented nested-array removal with a following sibling, quoted-key rename with sibling, nested insertion of a parsed compound value, and the exact CRLF nested-insert regression. Exact text is checked before timing; semantic results are independently parsed with the pure backend and checked for complete relevant value/type/order/sibling invariants. The final timed result is checked after each loop. Insert values and source fixtures are constructed before timing.

This Stopwatch microbenchmark is single-process and does not estimate confidence intervals or allocations. Run Release builds on an idle machine and compare repeated runs; measurements include API/interop overhead.
