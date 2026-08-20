# HARC Aspire Host Instructions

Bu repository HARC'ın .NET Aspire AppHost ve ServiceDefaults projelerini içerir. Ortak kurallar `.github/instructions/` altındaki coding, architecture, security ve documentation dosyalarındadır.

## Proje kuralları

- PostgreSQL, API, gateway ve frontend resource graph'ını koru.
- `WithReference()` ile sağlanan service discovery/configuration akışını bozma.
- Aspire portlarının standalone portlardan farklı olabileceğini dikkate al.
- OpenTelemetry, HTTP resilience ve health check yapılandırmalarını servis etkileriyle birlikte değerlendir.
- AppHost build'i ortam/SDK/NuGet kaynaklı başarısız olursa bunu kod hatası gibi raporlama.
