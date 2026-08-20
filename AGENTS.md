# HARC Aspire Host Agent Instructions

Bu repository'de çalışırken `.github/copilot-instructions.md` ve `.github/instructions/` altındaki ortak talimatları uygula.

- Aspire AppHost'un PostgreSQL, API, gateway ve frontend orchestration akışını koru.
- `WithReference()` ve service discovery configuration'ını değiştirirken dashboard endpoint'lerini kontrol et.
- Sabit standalone portları Aspire runtime portlarıyla karıştırma.
- ServiceDefaults telemetry, resilience ve health davranışını değiştirirken tüm servisleri değerlendir.
- Doğrulama sonrası `dotnet build` ile AppHost ve ServiceDefaults projelerini kontrol et.
