# Labirent: Kayıp Hazineler

Kayan koridorlu, piksel sanatlı bir zindan labirenti oyunu. Anahtarı bul, canavarları alt et, çıkış kapısına ulaş. Her 5. katta anahtar Minotor'dadır.

- `index.html`: oyunun ilk tarayıcı sürümü
- `kayiphazineler/`: Unity 6 (6000.6.2f1, URP) sürümü

## Unity sürümünü çalıştırma

1. `kayiphazineler` klasörünü Unity Hub ile aç.
2. `Assets/Scenes/SampleScene` sahnesini aç ve **Play**'e bas.

Ek kurulum yok: `Assets/Scripts/GameUI.cs` içindeki `Bootstrap`, oyun nesnesini ve kamerayı her sahnede kendiliğinden kurar. Spritelar, sesler ve arayüz kodla üretilir, harici varlık dosyası yoktur.

## Kontroller

| Eylem | Klavye | Dokunmatik |
|---|---|---|
| Hareket | WASD / Oklar | Sol alttaki joystick |
| Saldırı (Büyücü: büyü) | Boşluk / J | Saldır |
| Hızlı Saldırı | Q | Döner |
| Zıplama (Lv5) | E | Zıpla |
| Kalkan (Lv10) | R | Kalkan |
| Can iksiri | F | İksir |
| Fener (gizli bölmeleri gösterir) | L | Fener |
| Mola / karakter | Esc / P | II |

## Oyun özellikleri

- 4 sınıf: Şövalye, Barbar, Hırsız, Büyücü (her birinin kendi silahı ve özelliği var)
- Birkaç saniyede bir bir satır ya da sütun kayar, döner koridorlar yön değiştirir, kayar zeminler seni iter
- 7 canavar türü: goblin, iskelet, yarasa sürüsü, sürüngen, taş muhafız, duvardan geçen ruh, Minotor
- Gizli bölmeler, hazine sandıkları, değerli taşlar, altın keseleri
- Kat arası mağaza: silah, zırh, aksesuar, iksir
- Seviye atlama, yetenek açma, kayıtlı oyuna devam etme ve en iyi 10 skorluk rekor tablosu

## Kod yapısı (`kayiphazineler/Assets/Scripts`)

| Dosya | İçerik |
|---|---|
| `Data.cs` | Sınıflar, canavarlar, eşyalar, temalar, kayıt (PlayerPrefs) |
| `Game.cs` | Oyun kuralları: labirent üretimi, kayma, yapay zekâ, savaş |
| `GameView.cs` | Sprite tabanlı çizim ve kamera |
| `GameUI.cs` | Başlatma, girdi (Input System), HUD ve menüler |
| `PixelArt.cs` | Karakter dizilerinden çalışma anında üretilen piksel spritelar |
| `Sfx.cs` | Çalışma anında sentezlenen ses efektleri |
