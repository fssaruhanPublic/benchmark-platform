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
- **Senaryolar** — değerlendirme setleri, yük testleri ve kullanım raporları (aşağıya bakın): koşu listesi,
  vaka tablosu, metrik trendi ve iki koşunun metrik/vaka bazında karşılaştırması.

## Senaryo koşuları (`scenario-run/v1`)

BenchmarkDotNet dışındaki ölçümler — doğruluk/tutarlılık setleri, yük testleri, kullanım raporları — bu
belgeyle gelir. Dashboard üreticiyi tanımaz, üretici de dashboard'a referans vermez; tek bağ bu JSON.

```json
{
  "schema": "scenario-run/v1",
  "kind": "eval",                    // eval | load | usage | ...
  "suite": "dummydata-eticaret",     // trend ve karşılaştırma yalnızca aynı set içinde
  "label": "prompt v2",
  "startedAt": "2026-09-28T10:00:00Z",
  "finishedAt": "2026-09-28T10:03:12Z",
  "gitCommit": "abc1234", "gitBranch": "main",
  "machineName": "ci", "target": "http://localhost:5000",
  "environment": { "model": "..." },
  "metrics": [ { "name": "accuracy", "value": 0.92, "unit": "ratio", "direction": "higher" } ],
  "cases": [
    { "name": "toplam-siparis", "group": "sayma", "success": true, "message": null,
      "metrics": [ { "name": "latency.mean", "value": 1830, "unit": "ms", "direction": "lower" } ],
      "details": { "serbest": "JSON" } }
  ]
}
```

- `direction`: `higher` / `lower` / `none`. Karşılaştırma yalnızca yönü bilinen metrikleri
  kötüleşme/iyileşme olarak işaretler; ayrıca sonucu değişen vakaları (başarılı → başarısız) listeler.
- `unit` serbest metindir; arayüz `ratio`, `ms`, `s`, `USD`, `bytes`, `tokens`, `users`, `count` birimlerini
  biçimlendirir.
- Doğrulama katıdır (NaN, yinelenen vaka adı, bilinmeyen yön reddedilir): bu koşular kanıt olarak
  kullanılıyor ve sessizce düzeltilen bir değer karşılaştırmayı yanıltır.

BenchmarkDotNet'i bu projenin exporter'ı olmadan kullanan projeler, kendi `JsonExporter.Full`
raporlarını `POST /api/runs/import/bdn` ile gönderebilir; sonuçlar normal koşu olarak görünür.
Aynı `runId` ile gönderilen dosyalar tek koşuda toplanır. (BenchmarkDotNet JSON'u baseline bilgisini
taşımadığı için bu yoldan gelen koşularda **Oran** sütunu boş kalır.)

`Ingest:ApiKey` ayarlanırsa iki içe aktarma ucu da `X-Api-Key` başlığını ister.

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
| `POST /api/runs/import/bdn?runId&label&gitCommit&gitBranch&machineName` | BenchmarkDotNet `JsonExporter.Full` raporunu içe alır |
| `POST /api/scenario-runs` | `scenario-run/v1` belgesini kaydeder |
| `GET /api/scenario-runs?kind&suite&skip&take` | Senaryo koşuları |
| `GET /api/scenario-runs/{id}` | Koşu detayı: metrikler, ortam, vakalar |
| `DELETE /api/scenario-runs/{id}` | Koşuyu siler |
| `GET /api/scenario-runs/suites` | Setler, metrik ve vaka adları |
| `GET /api/scenario-runs/history?suite&metric&case&limit` | Bir metriğin (koşu ya da vaka düzeyinde) zaman serisi |
| `GET /api/scenario-runs/compare?baselineRunId&targetRunId&threshold` | İki senaryo koşusunun karşılaştırması |
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
