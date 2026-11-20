// Las pruebas de integración usan la misma BD; se ejecutan en serie para no competir por bloqueos.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

