# HARC AI Project Guide

Bu dosya, HARC kod tabanında çalışacak yeni bir geliştirici veya AI ajanı için teknik çalışma sözleşmesidir. Buradaki bilgiler kaynak kodun mevcut davranışından çıkarılmıştır; gelecekte kod değiştiğinde bu dosya da güncellenmelidir.

## 1. Projenin amacı ve mevcut durum

HARC, çalışanların kimlik bilgilerini, ekip/unvan ilişkilerini ve izin süreçlerini yöneten kurumsal bir İK portalıdır. Repository dört ana parçadan oluşur:

- `harc-fe`: React 19 + TypeScript + Vite SPA.
- `harc-gateway`: YARP tabanlı ASP.NET Core reverse proxy.
- `harc-api`: FastEndpoints ve EF Core kullanan .NET 10 API.
- `harc-aspire-host`: PostgreSQL, API, gateway ve frontend’i birlikte başlatan .NET Aspire AppHost.

Mevcut uygulamada çalışan ana işlevler Google ID token ile oturum doğrulama, kullanıcı profilini getirme, izin takvimi, izin bakiyesi ve izin talebi oluşturmadır. Payroll ve documents ekranları frontend’de route olarak bulunur; bunların tamamlanmış backend özellikleri yoktur.

## 2. Repository haritası

```text
HARC/
├─ README.md                         # İnsan odaklı sistem genel bakışı
├─ AI_PROJECT_GUIDE.md               # AI/developer teknik çalışma rehberi
├─ harc-api/                         # Backend API ve EF Core modeli
│  ├─ Program.cs                     # DI, DB, auth, FastEndpoints pipeline
│  ├─ Common/                         # Ortak entity ve exception tipleri
│  ├─ Modules/Identity/               # Kullanıcı, rol, takım, unvan
│  ├─ Modules/Leave/                  # İzin entity ve feature’ları
│  ├─ Modules/Document/               # Dosya yükleme servisi ve metadata
│  └─ docker-compose.yml              # Standalone PostgreSQL
├─ harc-gateway/                     # YARP proxy
├─ harc-fe/                          # React SPA
└─ harc-aspire-host/
   ├─ Harc.AppHost/                  # Dağıtık uygulama tanımı
   └─ Harc.ServiceDefaults/          # OpenTelemetry, discovery, health defaults
```

## 3. Çalışma zamanı ve istek akışı

### Standalone akış

```text
Browser (Vite)
   │ /api/* + Authorization: Bearer <Google ID token>
   ▼
YARP Gateway (genellikle 5000/5001)
   │ aynı /api path’i
   ▼
Harc API (launch profile’e göre 5100/5101)
   │ EF Core / Npgsql
   ▼
PostgreSQL
```

Gateway `/api/{**catch-all}` rotasını backend destination’a iletir. Gateway JWT doğrulamaz; doğrulama API’de yapılır.

### Aspire akışı

`Harc.AppHost/AppHost.cs` PostgreSQL’i ekler, API’ye veritabanı referansı verir, gateway’i API’ye bağlar ve frontend’i `bun ... dev` ile başlatır. `WithReference()` service discovery/configuration bilgilerini runtime’a sağlar. Aspire çalıştırıldığında portlar sabit standalone portlardan farklı olabilir; dashboard’daki endpoint’ler esas alınmalıdır.

## 4. Mimari kararlar

### Backend: modüler monolith + dikey dilimler

Tek bir API host’u vardır; iş alanları `Modules/` altında ayrılır. Her feature kendi endpoint, request ve response tiplerini aynı klasörde tutar. Bu yapı yeni bir modülü ayrı servis yapmadan izole etmeyi sağlar.

### FastEndpoints

Endpoint’ler controller yerine `Endpoint<TRequest, TResponse>` veya `EndpointWithoutRequest<TResponse>` sınıflarıdır. Route, tag, upload ve handler davranışı endpoint’in `Configure()`/`HandleAsync()` metotlarındadır.

### Tek DbContext

`IdentityDbContext` şu anda Identity, Leave, Document ve Common tablolarını aynı context üzerinden yönetir. Varsayılan şema `identity`; diğer entity’ler açıkça `leave`, `document` ve `common` şemalarına map edilir.

### Merkezi gateway

Frontend tek bir base URL kullanır. YARP, ileride backend servisleri çoğalsa bile browser’ın tek giriş noktasına bağlanmasını sağlar.

### Frontend server-state ayrımı

React Context global istemci durumunu (token, kullanıcı, dil, tema) taşır. TanStack React Query API verisini, cache’i, retry politikasını ve mutation invalidation’ını yönetir.

## 5. Authentication ve claims

Frontend Google OAuth callback’inden aldığı ID token’ı `localStorage.google_id_token` içine yazar ve API’ye Bearer token olarak gönderir. API `Program.cs` içinde Google authority’sine bağlı JWT Bearer authentication kullanır:

- Authority: `https://accounts.google.com`
- Issuer doğrulaması etkin.
- Audience, `Authentication:Google:ClientId` ile doğrulanır.
- Lifetime doğrulaması etkin.
- `MapInboundClaims = false` olduğu için raw claim isimleri kullanılır.

Authentication başarılı olduktan sonra `ClaimsTransformation` veritabanında `Users.Email` eşleşmesi arar. Kullanıcı yoksa otomatik oluşturmaz ve `ERR_USER_NOT_FOUND` business exception fırlatır. Terminated kullanıcı `ERR_ACCOUNT_TERMINATED` ile reddedilir.

Üretilen/korunan claim’ler:

| Claim | Kaynak | Kullanım |
|---|---|---|
| `email` | Google token | Kullanıcı lookup ve response |
| `name` | Google token | Audit `CreatedBy`/`UpdatedBy` fallback’i |
| `role` | `User.Role.Name` | Yetki bilgisi ve `/me` response |
| `harc_user_id` | `User.Id` | Backend’in güvenilir kullanıcı kimliği |

Bir token zaten `role` claim’i içeriyorsa transformation erken döner. Yeni endpoint’ler kullanıcı kimliğini request body’den almamalı; `harc_user_id` claim’inden okumalıdır.

## 6. API sözleşmesi

Tüm endpoint’ler `/api` prefix’i kullanır ve normal kullanımda Bearer token gerektirir.

### `GET /api/identity/me`

Kullanıcının claim ve ilişkisel profil bilgisini döndürür:

```json
{
  "internalUserId": "guid",
  "userEmail": "person@example.com",
  "assignedRole": "Employee",
  "assignedRoleDisplayName": { "tr": "Çalışan", "en": "Employee" },
  "avatar": "https://...",
  "team": { "id": 1, "name": "Engineering", "displayName": {} },
  "title": { "id": 1, "name": "Software_Engineer", "displayName": {} },
  "manager": null
}
```

### `POST /api/leave`

`multipart/form-data` kabul eder. Alanlar `StartDate`, `EndDate`, `LeaveType`, `Description` ve tekrar eden `Documents` dosya alanıdır. `LeaveType`: `1 Annual`, `2 Sick`, `3 Excuse`, `4 Unpaid`.

Mevcut handler kullanıcı claim’inden yeni `Pending` izin kaydı açar, `Days` değerini takvim günü olarak `(EndDate - StartDate) + 1` hesaplar ve dosyaları diske/metadata tablosuna kaydeder. Çakışma, hafta sonu, resmi tatil, bakiye ve tarih doğrulamaları henüz uygulanmış değildir.

### `GET /api/leave/calendar?year=YYYY&month=M`

Kullanıcının aynı aydaki izinlerini, aynı takımdaki diğer kullanıcıların izinlerini ve `common.Holidays` kayıtlarını döndürür. Ay filtresi `StartDate` üzerinden yapılır; çok aya taşan izinler yalnızca başlangıç ayında görünür.

### `GET /api/leave/my-balance`

Kullanıcıya ait `Approved + Annual` izinleri kullanılmış gün olarak toplar. `LeaveSettings` içindeki eşik ve yıllık hak değerlerini kullanarak geçmiş şirket yılı başına toplam kota hesaplar; kayıt yoksa 15 yıl / 20 gün / 25 gün fallback’i kullanır. Response alanları `totalLeaveQuota`, `usedLeaveDays`, `remainingLeaveDays`, `nextAnniversaryDate`, `daysUntilNextAnniversary`, `nextAllowanceAmount` değerleridir.

## 7. Veri modeli ve veritabanı

`IdentityDbContext` `BaseEntity` türevlerinde audit uygular:

- Insert: `CreatedAt`, `CreatedBy`.
- Update: `UpdatedAt`, `UpdatedBy`.
- Delete: fiziksel silme yerine `DeletedAt` doldurulup update’e çevrilir.

Mevcut entity’ler:

- `identity.Users`: Guid kullanıcı, unique email, role/title zorunlu, team ve manager ilişkileri, çalışma tarihleri, deneyim ve status.
- `identity.Roles`: unique `Name`, jsonb `DisplayName`.
- `identity.Teams`: unique `Name`, jsonb `DisplayName`.
- `identity.Titles`: unique `Name`, jsonb `DisplayName`.
- `leave.Leaves`: kullanıcı, tarih aralığı, gün sayısı, tür ve durum.
- `leave.LeaveSettings`: deneyim eşiği ve yıllık kota değerleri.
- `document.Documents`: dosya adı/yolu, MIME type, boyut, owner ve ilişkili entity metadata’sı.
- `common.Holidays`: tatil adı ve tarihi.

İlişki silme politikaları: Role/Title/Manager için `Restrict`, Team için `SetNull`. Migration dosyaları `harc-api/Modules/Identity/Data/Migrations/` altındadır; model değiştirildiğinde yeni migration oluşturulmalıdır.

## 8. Dosya yükleme

`DocumentManager`, dosyaları API’nin `wwwroot/uploads/documents` dizinine GUID tabanlı isimle yazar. Orijinal dosya adı, MIME type, boyut, owner, `DocumentType` ve `RelatedEntityId` `document.Documents` tablosuna kaydedilir. Dosya tipi, boyutu, virüs taraması, path güvenliği ve transaction/rollback politikası mevcut kodda sınırlıdır; yeni geliştirmelerde bunlar güvenlik gereksinimi olarak ele alınmalıdır.

## 9. Frontend sözleşmesi

- Başlangıç: `src/main.tsx` → `App.tsx`.
- Provider’lar: language, Google OAuth, TanStack Router; auth provider’ın uygulama ağacına eklenmesi ayrıca doğrulanmalıdır.
- Rotalar: `/login`, `/dashboard/home`, `/dashboard/profile`, `/dashboard/time-off`, `/dashboard/payroll`, `/dashboard/documents`, `*`.
- Dashboard route’u `localStorage` içindeki `google_id_token` ve `auth_user` ile korur.
- `AppInit`/`useGetMe`, token varsa `/api/identity/me` çağrısıyla session’ı doğrular; 401’de localStorage’ı temizleyip login’e yönlendirir.
- `apiClient`, `VITE_GATEWAY_BASE_URL`, Bearer header ve `Accept-Language` header’ını merkezi yönetir. FormData gönderiminde `Content-Type` manuel yazılmaz.
- `preferred_language`, `google_id_token` ve `auth_user` localStorage anahtarlarıdır.
- React Query key’leri arasında `auth/me`, `leave/my-balance` ve `leave/calendar/year/month` bulunur.

Frontend environment değişkenleri:

```env
VITE_GATEWAY_BASE_URL=http://localhost:5000
VITE_GOOGLE_CLIENT_ID=your-google-client-id
```

## 10. Gateway ve Aspire

Gateway’in tek YARP route’u `/api/{**catch-all}` eşleşmesini `http://localhost:5100` backend destination’ına yollar. `UseForwardedHeaders` `X-Forwarded-For` ve `X-Forwarded-Proto` işler. CORS policy mevcut kodda tüm origin’lere izin veren wildcard davranışındadır ve credentials açıktır; production için daraltılmalıdır.

`Harc.ServiceDefaults` OpenTelemetry instrumentation, OTLP exporter (endpoint varsa), service discovery, HTTP resilience ve self health check tanımlar. `MapDefaultEndpoints()` metodu health endpoint’lerini yalnız Development’ta map eder; mevcut API ve gateway pipeline’ında bu metot çağrılmadığı için endpoint’lerin erişilebilir olduğu varsayılmamalıdır.

## 11. Yeni feature ekleme prosedürü

1. İlgili bounded context’i seç (`Modules/Identity`, `Leave`, `Document` veya yeni modül).
2. Entity/ilişki değişikliğini `IdentityDbContext` mapping’iyle birlikte yap.
3. `dotnet ef migrations add <Name> --context IdentityDbContext` ile migration üret.
4. Feature klasöründe endpoint, request ve response tiplerini oluştur.
5. Kimlik için claim’den `harc_user_id` oku; request’ten gelen owner/user id’ye güvenme.
6. Frontend’de API fonksiyonu, TypeScript response tipi, React Query hook’u ve gerekiyorsa mutation invalidation ekle.
7. Route ve UI bileşenini mevcut layout/translation yaklaşımına uydur.
8. API/gateway build, frontend build/lint ve manuel endpoint kontrolü yap.
9. README ve bu rehberde endpoint, veri modeli ve bilinen sınırlamaları güncelle.

## 12. Bilinen sorunlar ve teknik borçlar

- Eski API README’sinde bulunmayan `login-google` endpoint’i anlatılıyordu; gerçek login akışı `/me` üzerindedir.
- Kullanıcı otomatik oluşturulmaz; veritabanında önceden bulunmalıdır.
- `CreateLeave` içindeki iş kuralları yorum seviyesindedir.
- Gateway CORS production için fazla geniştir.
- `appsettings.json` içinde örneklenmemesi gereken connection/auth bilgileri bulunabilir; secret yönetimi kullanılmalıdır.
- Migration’da Document–Leave shadow foreign key ilişkisi oluşmuş olabilir; model ve migration birlikte incelenmeden değiştirilmemelidir.
- Health check helper’ı mevcut olsa da endpoint mapping pipeline’a eklenmemiştir.
- API build sırasında `Microsoft.OpenApi` için güvenlik uyarısı alınabilir.
- Otomatik test projesi yoktur.
- Frontend build’i mevcut TypeScript sürümüyle `tsconfig.app.json` içindeki kaldırılmış `baseUrl` seçeneği nedeniyle başarısız olabilir.
- Frontend’de payroll/documents ekranları route seviyesinde mevcut olsa da tam backend akışı yoktur.

## 13. AI göreve başlamadan önce kontrol listesi

- [ ] İlgili feature’ın tüm endpoint, entity, request/response ve frontend hook dosyalarını okudum.
- [ ] `IdentityDbContext` mapping ve son migration’ı kontrol ettim.
- [ ] Kullanıcı kimliğini claim’den alacağım.
- [ ] Mevcut API response casing ve enum değerlerini koruyacağım.
- [ ] Dosya yüklemede FormData ve tekrar eden `Documents` key davranışını bozmayacağım.
- [ ] Config/secrets değerlerini dokümana veya koda gömmeyeceğim.
- [ ] Kod ile README arasında fark varsa README’yi gerçek davranışa göre güncelleyeceğim.
- [ ] İlgili build/lint/manual testleri çalıştıracağım.
