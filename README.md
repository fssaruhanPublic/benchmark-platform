# Benchmark Platform

BenchmarkDotNet ile yazılan performans testleri ve sonuçlarını otomatik toplayan bir dashboard.
Bir koşu bittiği anda sonuçlar SQLite'a yazılır; dashboard'da ayrıca bir içe aktarma adımı yoktur.

```
src/
  BenchmarkPlatform.Core/        Entity'ler, EF Core DbContext, sorgu servisleri
  BenchmarkPlatform.Reporting/   BenchmarkDotNet exporter + ortak config
  BenchmarkPlatform.Benchmarks/  Benchmark'ların yazıldığı konsol uygulaması
  BenchmarkPlatform.Api/         Minimal API (+ derlenmiş arayüzü wwwroot'tan servis eder)
  web/                           Vite + React + TypeScript dashboard
tests/
  BenchmarkPlatform.Tests/       xUnit testleri
data/benchmarks.db               SQLite veritabanı (git'e girmez)
```

## Hızlı başlangıç

```bash
dotnet build
```

### 1. Benchmark çalıştır

```bash
dotnet run -c Release --project src/BenchmarkPlatform.Benchmarks -- --filter "*" --label "ilk kosu"
```

Koşu bitince konsolda şuna benzer bir satır görürsünüz:

```
[Dashboard] Kayit tamam - bu kosuda toplam 21 sonuc. Run Id: ..., DB: ...\data\benchmarks.db
```

Faydalı argümanlar (tümü BenchmarkDotNet'in kendi argümanlarıdır, `--label` hariç):

| Argüman | Açıklama |
|---|---|
| `--filter "*StringConcat*"` | Sadece eşleşen benchmark'ları çalıştırır (`--filter "*"` = hepsi) |
| `--job short` | Hızlı ama daha az hassas ölçüm — geliştirme sırasında pratik |
| `--list flat` | Mevcut benchmark'ları listeler |
| `--label "önbellek öncesi"` | Koşuya dashboard'da görünecek bir isim verir |

### 2. Dashboard'ı aç

Geliştirme (API + Vite dev sunucusu, hot reload):

```bash
dotnet run --project src/BenchmarkPlatform.Api
```

```bash
cd src/web && npm install && npm run dev
```

Arayüz <http://localhost:5173>, API <http://localhost:5080> adresinde çalışır; Vite `/api` isteklerini API'ye yönlendirir.

Tek uygulama olarak (arayüz API tarafından servis edilir):

```bash
cd src/web && npm run build
```

```bash
dotnet run --project src/BenchmarkPlatform.Api
```

Ardından <http://localhost:5080> adresini açın. `npm run build` çıktısı doğrudan
`src/BenchmarkPlatform.Api/wwwroot` klasörüne yazılır.

## Yeni benchmark yazma

`src/BenchmarkPlatform.Benchmarks/Benchmarks/` altına bir sınıf ekleyin — başka hiçbir kayıt işlemi gerekmez,
`BenchmarkSwitcher` derlemedeki tüm benchmark sınıflarını bulur ve ortak `DashboardConfig`'i uygular:

```csharp
using BenchmarkDotNet.Attributes;

namespace BenchmarkPlatform.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("my-feature")]
public class MyFeatureBenchmarks
{
    [Params(100, 10_000)]        // aynı benchmark farklı girdilerle koşar
    public int Size { get; set; }

    private int[] _data = [];

    [GlobalSetup]                // ölçüme dahil edilmez
    public void Setup() => _data = Enumerable.Range(0, Size).ToArray();

    [Benchmark(Baseline = true)] // diğerleri buna oranlanır
    public long CurrentImplementation() => _data.Sum(x => (long)x);

    [Benchmark]
    public long NewImplementation()
    {
        long sum = 0;
        foreach (var value in _data) sum += value;
        return sum;
    }
}
```

Notlar:

- Sınıfı bir kütüphaneyi ölçmek için kullanacaksanız o projeye `ProjectReference` ekleyin.
- `[Baseline = true]` verilen metot, dashboard'daki **Oran** sütununun referansıdır.
- Bir benchmark'ın kimliği `TypeName.MethodName(parametreler)` şeklinde oluşur; metot veya sınıf adını
  değiştirirseniz trend grafiğinde yeni bir seri olarak görünür.
- Ölçüm mutlaka `-c Release` ile yapılmalıdır.

## Dashboard ekranları

- **Koşular** — koşu listesi, özet sayaçlar, arama ve silme.
- **Koşu detayı** — sıralanabilir sonuç tablosu (ortalama, hata, std sapma, medyan, P95, ops/sn, Gen0, ayrılan bellek, baseline oranı) ve ortalama süre grafiği.
- **Trendler** — seçilen benchmark'ın koşular boyunca süre ve bellek değişimi.
- **Karşılaştır** — iki koşu arasında benchmark bazında yüzde fark; eşiği (varsayılan %10) aşanlar regresyon/iyileşme olarak işaretlenir.

## API

| Endpoint | Açıklama |
|---|---|
| `GET /api/runs?skip&take&search` | Koşu listesi |
| `GET /api/runs/{id}` | Koşu detayı ve sonuçları |
| `DELETE /api/runs/{id}` | Koşuyu ve sonuçlarını siler |
| `GET /api/benchmarks` | Bilinen benchmark'lar |
| `GET /api/benchmarks/history?fullName=&limit=` | Bir benchmark'ın zaman serisi |
| `GET /api/compare?baselineRunId=&targetRunId=&threshold=` | İki koşunun karşılaştırması |
| `GET /api/summary` | Dashboard özet sayaçları |
| `GET /health` | Sağlık kontrolü ve kullanılan veritabanı yolu |

## Veritabanı

Varsayılan konum `data/benchmarks.db`. `BENCHMARK_DB_PATH` ortam değişkeni ile değiştirilebilir
(hem exporter hem API bu değişkeni okur); API ayrıca `ConnectionStrings:Benchmarks` ayarını da kabul eder.
Şema EF Core migration'ları ile yönetilir ve hem exporter hem API açılışta `Migrate()` çağırır.

Şema değiştirdiğinizde:

```bash
dotnet ef migrations add <Ad> -p src/BenchmarkPlatform.Core -s src/BenchmarkPlatform.Core -o Data/Migrations
```

## Testler

```bash
dotnet test
```

## Lisans

MIT — bkz. [LICENSE](LICENSE).
