var builder = DistributedApplication.CreateBuilder(args);

// 1. Docker üzerinde PostgreSQL veritabanını ayarla
var postgres = builder.AddPostgres("postgres")
                      .WithDataVolume()
                      .AddDatabase("DefaultConnection", "harc_db");

// 2. API Projesini ekle ve veritabanını bağla
var api = builder.AddProject<Projects.harc_api>("api")
                 .WithReference(postgres);

// 3. Gateway Projesini ekle ve API'yi bağla
var gateway = builder.AddProject<Projects.harc_gateway>("gateway")
                     .WithReference(api);

// 4. React Frontend uygulamasını ekle
// "frontend-repo" kısmını kendi React projenizin ana klasör adıyla değiştirin
builder.AddExecutable("frontend", "bun", "../../harc-fe", "dev")
       .WithReference(gateway)
       .WithEnvironment("VITE_GATEWAY_BASE_URL", gateway.GetEndpoint("https"));

       
builder.Build().Run();
