# Blackjack

ASP.NET Core MVC tabanli bir Blackjack ve Poker egitim projesi.

## Ozellikler

- Blackjack oyunu
- Poker (Texas Hold'em) oyun akisi
- OpenRouter veya AnythingLLM uzerinden AI strateji yardimi
- Kart gorselinden kart tespiti (vision model)

## Gereksinimler

- .NET 8 SDK

## Kurulum

1. Projeyi klonla:
   ```bash
   git clone https://github.com/oguzhandede/BlackJack.git
   cd BlackJack
   ```
2. Yerel gizli ayar dosyasi olustur:
   ```bash
   cp appsettings.Local.example.json appsettings.Local.json
   ```
   Windows PowerShell icin:
   ```powershell
   Copy-Item appsettings.Local.example.json appsettings.Local.json
   ```
3. `appsettings.Local.json` icine AI saglayicisi secimini ve gerekli anahtarlari ekle:
   ```json
   {
     "AI": {
       "Provider": "AnythingLLM"
     },
     "OpenRouter": {
       "ApiKey": "YOUR_REAL_OPENROUTER_API_KEY"
     },
     "AnythingLLM": {
       "BaseUrl": "http://ai-dev.poyolms.com",
       "ApiKey": "YOUR_REAL_ANYTHINGLLM_API_KEY",
       "WorkspaceSlug": "poyo",
       "Mode": "query",
       "HistoryLimit": 100,
       "HistoryOrderBy": "asc"
     }
   }
   ```
   `AI:Provider` degeri `OpenRouter` veya `AnythingLLM` olabilir.
4. Uygulamayi calistir:
   ```bash
   dotnet run
   ```

## Guvenlik Notu

- `appsettings.json` dosyasinda sadece ornek/degerlendirme verisi bulunur.
- Gercek API anahtarlari `appsettings.Local.json` icinde tutulmalidir.
- `appsettings.Local.json` `.gitignore` ile repoya dahil edilmez.

## Lisans

Bu proje [MIT](LICENSE) lisansi ile lisanslanmistir.
