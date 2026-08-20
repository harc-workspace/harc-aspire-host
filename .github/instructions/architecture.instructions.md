# Architecture Instructions

- Yeni özelliği önce mevcut bounded context, module ve service sınırları içinde konumlandır.
- Mevcut abstraction'ları anlamadan yeni abstraction veya framework ekleme.
- API, frontend, gateway ve database sözleşmelerini birlikte değerlendir.
- Database model değişikliklerinde mapping, migration ve seed/config etkilerini kontrol et.
- Breaking change gerekiyorsa nedenini, etkilenen consumer'ları ve migration yolunu dokümante et.
- Distributed system veya service discovery davranışını local development kolaylığı için bozma.
- Mimari kararları kısa bir ADR veya ilgili teknik dokümanla kayda geçir.
