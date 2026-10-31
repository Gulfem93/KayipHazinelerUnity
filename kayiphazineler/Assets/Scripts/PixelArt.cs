using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KayipHazineler
{
    public class PixelSprite { public Sprite normal, white; }

    public class HeroSprites { public PixelSprite[] front, back; }

    // Piksel sanat: spritelar karakter dizileriyle yazılır, çalışma anında dokuya pişirilir.
    // Simetrik olanlar sol yarı olarak yazılır ve aynalanır.
    public static class PixelArt
    {
        public const int SZ = 24, HALF = 12;
        const string K = "#160f22";

        static string[] M(params string[] rows) => rows.Select(r =>
        {
            int half = r.Length <= 6 ? 6 : HALF;
            r = r.PadRight(half, '.').Substring(0, half);
            return r + new string(r.Reverse().ToArray());
        }).ToArray();

        static Dictionary<char, Color32> Pal(string spec, Dictionary<char, Color32> baseP = null)
        {
            var p = baseP != null ? new Dictionary<char, Color32>(baseP) : new Dictionary<char, Color32> { ['K'] = Data.C(K) };
            foreach (var part in spec.Split(' '))
                if (part.Length > 2) p[part[0]] = Data.C(part.Substring(2));
            return p;
        }

        static Texture2D NewTex(int w, int h, bool point = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = point ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return t;
        }

        public static PixelSprite Bake(string[] rows, Dictionary<char, Color32> pal, int size = SZ)
        {
            var list = rows.ToList();
            while (list.Count < size) list.Insert(0, "");
            var tex = NewTex(size, size);
            var wtex = NewTex(size, size);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    string r = list[y];
                    // Doku koordinatı alttan başlar, satırlar üstten yazılır
                    if (x < r.Length && pal.TryGetValue(r[x], out var c))
                    {
                        tex.SetPixel(x, size - 1 - y, c);
                        wtex.SetPixel(x, size - 1 - y, Color.white);
                    }
                    else { tex.SetPixel(x, size - 1 - y, clear); wtex.SetPixel(x, size - 1 - y, clear); }
                }
            tex.Apply(); wtex.Apply();
            var rect = new Rect(0, 0, size, size);
            return new PixelSprite
            {
                normal = Sprite.Create(tex, rect, new Vector2(.5f, .5f), size),
                white = Sprite.Create(wtex, rect, new Vector2(.5f, .5f), size)
            };
        }

        /* ---------- Kahramanlar ---------- */
        static readonly Dictionary<char, Color32> HeroPal = Pal(
            "H=#2a2340 h=#51468a j=#16121f S=#f3cfa6 s=#d6a077 E=#1a1226 w=#ffffff C=#2f62b8 c=#1e3f80 q=#5f97ec " +
            "T=#4a4560 t=#332f48 B=#8a5a2c G=#f5c04a P=#2d2840 O=#6a4426 M=#b4bfd0");

        static readonly string[] HeroFront = M(
            "............", "........KKKK", "......KKHHHh", ".....KHHHhhH", "....KHHhhHHH", "....KHHHHHHH",
            "....KHjHHHjj", "....KHSjSSSS", "....KHSSwESS", "....KjSSwESS", ".....KSSSSSS", ".....KsSSSSs",
            "......KKsSSS", ".....KqCKKss", "....KqCCCKTT", "...KqCCCKTTT", "...KCCCKMTTT", "...KCCKSKBBG",
            "...KCCcKtTTT", "...KCccKKtTT");
        static readonly string[] HeroBack = M(
            "............", "........KKKK", "......KKHHHh", ".....KHHHhhH", "....KHHhhHHH", "....KHHHHHHH",
            "....KHHHjHHH", "....KHjHHHjH", "....KHHHjHHH", "....KjHHHHjH", ".....KjHHHHH", ".....KKjjHHH",
            "......KKjjjj", ".....KqCKKCC", "....KqCCCCCC", "...KqCCCCCCC", "...KCCCCcCCC", "...KCKSKCcCC",
            "...KCCcCCCcC", "...KCccCCcCC");
        static readonly string[][] Legs =
        {
            new[] { "....KKK.KPPPPPPK.KKK....", "........KPPKKPPK........", "........KOOKKOOK........", "........KKKKKKKK........" },
            new[] { "....KKK.KPPPPPPK.KKK....", "........KPPKKPPK........", "........KOOKKPPK........", "........KKKKKOOK........" },
            new[] { "....KKK.KPPPPPPK.KKK....", "........KPPKKPPK........", "........KPPKKOOK........", "........KOOKKKKK........" },
        };
        static string[] BodyBack => HeroBack.Skip(13).ToArray();

        static HeroSprites HeroSet(string[] front, string[] back, Dictionary<char, Color32> pal) => new HeroSprites
        {
            front = Legs.Select(l => Bake(front.Concat(l).ToArray(), pal)).ToArray(),
            back = Legs.Select(l => Bake(back.Concat(l).ToArray(), pal)).ToArray(),
        };

        static Dictionary<string, HeroSprites> heroes;
        public static Dictionary<string, HeroSprites> Heroes
        {
            get
            {
                if (heroes != null) return heroes;
                heroes = new Dictionary<string, HeroSprites>
                {
                    ["knight"] = HeroSet(HeroFront, HeroBack, HeroPal),
                    ["barbarian"] = HeroSet(M(
                        "......K.....", ".....KhK.KKK", "....KHhHKHHh", "...KHHHhhHhH", "...KHHhhHHHH", "....KHHHHHHH",
                        "....KHjHHHjj", "....KHSjSSSS", "....KHSSwESS", "....KjSSwESS", ".....KHSSSSS", ".....KHHHSKK",
                        "......KHHHHH", ".....KqCKHHH", "....KqCCCKTT", "...KSqCCKTTT", "...KSSSKMTTT", "...KSSKSKBBG",
                        "...KSsKKtTTT", "...KKKKKKtTT"), M(
                        "......K.....", ".....KhK.KKK", "....KHhHKHHh", "...KHHHhhHhH", "...KHHhhHHHH", "....KHHHHHHH",
                        "....KHHHjHHH", "....KHjHHHjH", "....KHHHjHHH", "....KjHHHHjH", ".....KjHHHHH", ".....KKjjHHH",
                        "......KKjjjj", ".....KqCKKCC", "....KqCCCCCC", "...KSqCCCCCC", "...KSSCCcCCC", "...KSKSKCcCC",
                        "...KSsCCCCcC", "...KKKKCCcCC"),
                        Pal("H=#c0502a h=#ee8a44 j=#7e2c14 C=#7a5a3a c=#5a3e24 q=#a8865a T=#d29a70 t=#a8744e P=#4a3a2a O=#5a3a22", HeroPal)),
                    ["thief"] = HeroSet(M(
                        "...........K", "..........Kh", "........KKhH", "......KKHHHH", ".....KHHhHHH", "....KHHhHHHH",
                        "....KHHjjjjj", "....KHjSSSSS", "....KHjSwESS", "....KHjSwESS", "....KHHjjjjj", ".....KHjjjjj",
                        "......KKjjjj", ".....KqCKKjj", "....KqCCCKTT", "...KqCCCKTTT", "...KCCCKMTTT", "...KCCKSKBBG",
                        "...KCCcKtTTT", "...KCccKKtTT"), M(
                        "...........K", "..........Kh", "........KKhH", "......KKHHHH", ".....KHHhHHH", "....KHHhHHHH",
                        "....KHHHhHHH", "....KHHHHhHH", "....KHjHHHhH", "....KHHjHHHH", "....KHHHjHHH", ".....KHHHjHH",
                        "......KKHHHH").Concat(BodyBack).ToArray(),
                        Pal("H=#2f5a3a h=#4f845a j=#1b3424 C=#2f5a3a c=#1b3424 q=#5f9468 T=#4a3a2a t=#33281c B=#3d2716 G=#c9d2dc P=#2a2a30 O=#3a2a1e M=#6a5a4a", HeroPal)),
                    ["mage"] = HeroSet(M(
                        "...........K", "..........Kh", ".........KhH", "........KhHH", ".......KHHHH", "......KHHHHH",
                        "..KKKKKyyyyy", ".Kjjjjjjjjjj", "....KRSSSSSS", "....KRSSwESS", "....KRSSwESS", ".....KSSSSSs",
                        "......KKsSSS", ".....KqCKKss", "....KqCCCKTT", "...KqCCCKTTT", "...KCCCKTTTT", "...KCCKSKBBG",
                        "...KCCcKTTTT", "...KCccKTTTT"), M(
                        "...........K", "..........Kh", ".........KhH", "........KhHH", ".......KHHHH", "......KHHHHH",
                        "..KKKKKyyyyy", ".Kjjjjjjjjjj", "....KRRRRRRR", "....KRRRrRRR", "....KRrRRRrR", ".....KRRRRRR",
                        "......KKRRRR").Concat(BodyBack).ToArray(),
                        Pal("H=#5a3a9a h=#8a68d8 j=#33205e y=#f5c04a R=#ece6f8 r=#b8b0cc C=#4a3488 c=#2e1f5e q=#7a5ac8 T=#3a2a70 t=#2a1d52 B=#f5c04a G=#5fd0ff P=#4a3488 O=#2a1d52", HeroPal)),
                };
                return heroes;
            }
        }

        /* ---------- Canavarlar ve nesneler ---------- */
        static Dictionary<string, PixelSprite> sprites;
        public static PixelSprite Get(string id)
        {
            if (sprites == null) BuildSprites();
            return sprites.TryGetValue(id, out var s) ? s : null;
        }

        static void BuildSprites()
        {
            sprites = new Dictionary<string, PixelSprite>
            {
                ["goblin"] = Bake(M(
                    "............", "............", "............", "............", ".KK......KKK", ".KgK...KKggg",
                    "..KgKKKggggg", "..KlgKgggggg", "...KggggKKgg", "....KgKYEKgg", "....KggKKggg", "....KGgggggg",
                    ".....KGgKWKW", ".....KGGKKKK", "......KKGGGG", "....KgKKnnnn", "...KgGKnNnnn", "...KgGKnNnnn",
                    "...KlgKbbbbb", "....KKKnNnnn", "......KnnnKK", ".....KggK...", ".....KGGK...", ".....KKKK..."),
                    Pal("g=#6cc04a G=#4a8f34 l=#a6e47a Y=#ffe14a E=#1a1226 W=#f4f0e0 n=#7a4f2a N=#57361b b=#3d2716")),
                ["skeleton"] = Bake(M(
                    "............", "............", ".......KKKKK", ".....KKwwwww", "....Kwwwwwww", "...Kwwwwwwww",
                    "...KwwKKKwww", "...KwKKRKwww", "...KswKKKwwK", "....KsswwwwK", ".....KswKwKw", "......KKKKKK",
                    "..........Kw", ".....KKKKKKw", "....KwwKwwKw", "...KwK.KswKw", "...Kw..KwwKw", "...Kw..KswKw",
                    "...KwK..KKKw", "........KwwK", "........KwK.", "........KwK.", ".......KwwK.", ".......KKKK."),
                    Pal("w=#efeadb s=#b9b19c R=#ff5a3a")),
                ["bat"] = Bake(M(
                    "............", "............", "............", "............", "............", "............",
                    "K...........", "KK..........", "KpK.....KK..", "KppK....KpK.", "KpppKK.Kpppp", "KPpppKKpppRp",
                    "KPPpppKppppp", ".KPPpppKpWpp", "..KPK.KPKppp", ".........KKK", "............", "............"),
                    Pal("p=#8a5ad0 P=#5a3494 R=#ff4a5a W=#f4f0e0")),
                ["spider"] = Bake(M(
                    "........KKKK", "......KKVVVV", "..KK.KVuuVVV", ".K..KKVuVVVv", "K..K.KVVVVvv", "K.K..KKVVvvv",
                    ".K.KKKKKKKKK", "K..K.Kvvvvvv", "K.K..KvRvvRv", ".K..KKvvvvvv", ".K.K.KKvvvKW", "K..K..KKKKKK",
                    "K...K.......", ".....K......"),
                    Pal("v=#5a2d8a V=#9a5ad8 u=#c79af0 R=#ff3a4a W=#f4f0e0")),
                ["golem"] = Bake(M(
                    "........KKKK", "......KKrrrr", ".....KrrrrrR", ".....KrRRrrr", ".....KrKYYKr", ".....KRrrrrr",
                    "..KKKKKRRDDD", ".KrrrKrrrrrr", "KrrrrKrmmrrr", "KrRrrKrrmrRr", "KrRRrKRrrrRr", "KrrrrKRRrrrr",
                    "KRrrKKDRRRRR", "KrrrrK.KrrrK", "KRRRRK.KrRRK", ".KKKK.KrrK..", "......KRRK..", ".....KKKKK.."),
                    Pal("r=#9a9aa8 R=#6a6a7a D=#4a4a58 m=#6fae4a Y=#ffb02a")),
                ["boss"] = Bake(M(
                    "h...........", "hh..........", ".hH.........", ".hhH...KKKKK", "..hhHKKooooo", "...KhKoooooo",
                    "...KoooOoooo", "...KooRKoooo", "....KooooKKK", "....KoOOKnnn", "....KoOKnnKn", ".....KOKnnnG",
                    "..KKKKKOOKGK", ".KoolKKoooKK", "KooolKoooooo", "KoolKKlooooo", "KooK.KoOOOoo", "KnnK.KOOOOOO",
                    ".KK..KbbbbGb", ".....KOOOOOO", "......KooK..", "......KOOK..", ".....KhhhK..", ".....KKKKK.."),
                    Pal("o=#9a5230 O=#62321c l=#c07040 h=#efe3c8 H=#b8a98a R=#ff3a3a G=#f5c04a n=#d08070 b=#3d2716")),
                ["ghost"] = Bake(M(
                    "........KKKK", "......KKuuuu", ".....Kuuuuuu", "....Kuuuuuuu", "...KuuuuuUuu", "...KuuKKKuuu",
                    "...KuKEEwKuu", "...KuKEEEKuu", "...KuuKKKuuu", "...Kuuuuuuuu", "...KuuuuuKKK", "...KUuuuuKEE",
                    "...KUuuuuuKK", "...KUUuuuuuu", "...KUUUuuuuu", "...KUUUUuuuu", "...KUKUUUKUU", "...K.KUK.KUK",
                    "......K...K."),
                    Pal("u=#d9b8ff U=#9a6ad8 E=#2a1440 w=#ffffff")),
                ["chest"] = Bake(M(
                    "...KKKKKKKKK", "..Knlnnnnnnn", "..KnNnnnnnnn", "..KGGGGGGGGG", "..KnNnnnnKKK", "..KnNnnnnKGG",
                    "..KnNnnnnKGK", "..KnNnnnnnKK", "..KnNnnnnnnn", "..KGGGGGGGGG", "..KKKKKKKKKK", "............",
                    "............"),
                    Pal("n=#9a6230 N=#6e4220 l=#c08850 G=#f5c04a")),

                // 12x12 simgeler
                ["coin"] = Bake(M(
                    "....KK", "..KKGG", ".KGGYY", ".KGYGG", "KGYGGG", "KGYGGG", "KGYGGG", "KGGGGG", ".KGGGG", ".KgGGG",
                    "..KKgg", "....KK"), Pal("K=#5a3a10 G=#f5c04a Y=#fff0a0 g=#c08a20"), 12),
                ["gem"] = Bake(M(
                    "......", "...KKK", "..KuuU", ".KuuUU", "KuwUUU", "KUUUUU", ".KUUUU", "..KUUU", "...KUU", "....KU",
                    ".....K", "......"), Pal("u=#e3c4ff U=#a34ee8 w=#ffffff"), 12),
                ["bag"] = Bake(M(
                    "....KK", "...KbK", "....KK", "...KnN", "..KnnN", ".KnnnN", "KnnGnn", "KnnnGG", "KnnnnG", "Knnnnn",
                    ".Knnnn", "..KKKK"), Pal("n=#b08350 N=#8a6030 b=#3d2716 G=#f5c04a"), 12),
                ["key"] = Bake(new[] {
                    "............", "............", "............", ".KKKK.......", "KGGGGK......", "KG..GKKKKKKK",
                    "KG..GGGGGGGK", "KGGGGKKGKGKK", ".KKKK..K.K..", "............", "............", "............" },
                    Pal("G=#ffd84a"), 12),
                ["potion"] = Bake(M(
                    "....KK", "....Kc", "....Kc", "...KKK", "..KrrR", ".KrrRR", "KrwrRR", "KrrRRR", "KrRRRR", ".KrRRR",
                    "..KKKK", "......"), Pal("c=#b08350 r=#ff6a6a R=#d02a3a w=#ffffff"), 12),
                ["door"] = Bake(M(
                    "..KKKK", ".KssKK", "KssKdd", "KsKddd", "KsKdDd", "KsKdDd", "KsKdDd", "KsKdDY", "KsKdDd", "KsKdDd",
                    "KsKddd", "KKKKKK"), Pal("s=#8a8a9a d=#6e4220 D=#57361b Y=#f5c04a"), 12),
                ["door_open"] = Bake(M(
                    "..KKKK", ".KssKK", "KssKyy", "KsKyyw", "KsKyww", "KsKyww", "KsKyww", "KsKyww", "KsKyww", "KsKyww",
                    "KsKyyy", "KKKKKK"), Pal("s=#8a8a9a y=#ffb02a w=#fff4b0"), 12),
            };
        }

        /* ---------- Basit şekiller ---------- */
        static Sprite square, circle, ring, chevron, glow;
        public static Sprite Square => square ? square : square = MakeShape(4, (x, y) => 1f, true);
        public static Sprite Circle => circle ? circle : circle = MakeShape(64, (x, y) => Edge(.5f - Mathf.Sqrt(x * x + y * y), 64), false);
        public static Sprite Ring => ring ? ring : ring = MakeShape(64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Min(Edge(.5f - r, 64), Edge(r - .40f, 64));
        }, false);
        public static Sprite Glow => glow ? glow : glow = MakeShape(64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y) * 2f;
            return Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
        }, false);
        // Sağı gösteren ok başı
        public static Sprite Chevron => chevron ? chevron : chevron = MakeShape(32, (x, y) =>
        {
            float a = Mathf.Abs(y) + (x + .1f);
            return a > -.05f && a < .15f && x < .25f ? 1f : 0f;
        }, true);

        static float Edge(float d, int size) => Mathf.Clamp01(d * size + .5f);

        // f: merkezden -0.5..0.5 aralığında koordinat alır, opaklık döner
        static Sprite MakeShape(int size, System.Func<float, float, float> f, bool point)
        {
            var t = NewTex(size, size, point);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, f((x + .5f) / size - .5f, (y + .5f) / size - .5f)));
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        public static Texture2D Solid(Color c)
        {
            var t = NewTex(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
