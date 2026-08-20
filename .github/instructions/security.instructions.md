# Security Instructions

- Secret, token, private key, gerçek connection string ve production credential'ı commit etme.
- Kullanıcı kimliğini client tarafından gönderilen owner/user id değerine göre belirleme; güvenilir authenticated claim/context kullan.
- Authentication, authorization ve tenant/ownership kontrollerini endpoint seviyesinde doğrula.
- Input validation, upload validation, path traversal ve injection risklerini değerlendir.
- Log'lara access token, password, kişisel veri veya hassas header yazma.
- CORS, forwarded headers, cookie/token storage ve file upload değişikliklerinde production etkisini kontrol et.
- Güvenlik açığı veya şüpheli config bulursan bunu değişiklik özetinde açıkça belirt.
