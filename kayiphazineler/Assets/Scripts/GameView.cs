using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KayipHazineler
{
    // Sprite havuzu: her karede baştan "çizilir", kullanılmayan rendererlar gizlenir
    public class SpritePool
    {
        readonly Transform root;
        readonly List<SpriteRenderer> list = new List<SpriteRenderer>();
        int used;

        public SpritePool(string name, Transform parent)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
        }

        public void Begin() => used = 0;

        public SpriteRenderer Draw(Sprite s, Vector2 pos, Vector2 scale, Color color, int order, float angle = 0, bool flip = false)
        {
            SpriteRenderer r;
            if (used < list.Count) r = list[used];
            else
            {
                r = new GameObject("s").AddComponent<SpriteRenderer>();
                r.transform.SetParent(root, false);
                list.Add(r);
            }
            used++;
            if (!r.enabled) r.enabled = true;
            r.sprite = s;
            r.color = color;
            r.sortingOrder = order;
            r.flipX = flip;
            var t = r.transform;
            t.localPosition = new Vector3(pos.x, pos.y, 0);
            t.localRotation = angle == 0 ? Quaternion.identity : Quaternion.Euler(0, 0, angle);
            t.localScale = new Vector3(scale.x, scale.y, 1);
            return r;
        }

        public void End()
        {
            for (int i = used; i < list.Count; i++) if (list[i].enabled) list[i].enabled = false;
        }
    }

    // Çizim: labirent tahtası, varlıklar ve efektler. Karo (x, y) dünyada (x, -y) köşesinden başlar.
    public partial class Game
    {
        Camera cam;
        SpritePool boardPool, dynPool;
        static readonly Color Bg = Data.C("#070b14");
        const int OBoard = 0, OWall = 200, OItem = 400, OEnt = 1000, OFx = 3000, OFrame = 5000;
        int boardHash = -1;

        void SetupView()
        {
            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = .1f; cam.farClipPlane = 100;
            // Sprite oyununda son işleme (bloom vb.) renkleri bozmasın
            var urp = cam.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null) urp.renderPostProcessing = false;
            boardPool = new SpritePool("Board", transform);
            dynPool = new SpritePool("Dynamic", transform);
        }

        // Oyun alanının en-boy oranı (yükseklik / genişlik), HUD ve kontroller hariç
        float BoardAspect()
        {
            var r = PlayRect();
            return r.height / Mathf.Max(1, r.width);
        }

        // Ekranın labirente ayrılan kısmı (piksel, sol-alt köken)
        Rect PlayRect()
        {
            float top = HudHeightPx(), bottom = ControlsHeightPx(), pad = 8 * UiScale;
            return new Rect(pad, bottom + pad, Screen.width - pad * 2, Screen.height - top - bottom - pad * 2);
        }

        Vector2 Cell(float x, float y) => new Vector2(x + .5f, -(y + .5f));

        void FitCamera()
        {
            var r = PlayRect();
            // Tahta ekranda PlayRect'e sığacak şekilde ölçeklenir
            float unitsPerPx = Mathf.Max((w + .4f) / r.width, (h + .6f) / r.height);
            cam.orthographicSize = Screen.height * unitsPerPx / 2;
            // Tahtanın merkezi PlayRect'in merkezine gelsin
            Vector2 boardCenter = new Vector2(w / 2f, -h / 2f - .1f);
            Vector2 offPx = r.center - new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 c = boardCenter - offPx * unitsPerPx;
            Vector2 sh = shake > 0 ? Random.insideUnitCircle * shake * .012f : Vector2.zero;
            cam.transform.position = new Vector3(c.x + sh.x, c.y + sh.y, -10);
        }

        // Kayma animasyonu sırasında bir karonun kayık konumu (ve sarılan kopyası)
        void Each(int x, int y, System.Action<Vector2> fn)
        {
            if (anim == null || !(anim.row ? y == anim.idx : x == anim.idx)) { fn(Vector2.zero); return; }
            float e = Ease(anim.t) * anim.dir;
            int len = anim.row ? w : h;
            // Izgara koordinatında: satır sağa, sütun aşağı (y artar) kayar
            Vector2 axis = anim.row ? new Vector2(1, 0) : new Vector2(0, 1);
            fn(axis * e);
            fn(axis * (e - anim.dir * len));
        }
        static float Ease(float t) => t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;

        void Render()
        {
            if (tiles == null) { boardPool.Begin(); boardPool.End(); dynPool.Begin(); dynPool.End(); return; }
            FitCamera();
            int hash = (w * 31 + h) * 17 + Screen.width * 7 + Screen.height;
            if (dirty || anim != null || hash != boardHash) { DrawBoard(); dirty = false; boardHash = anim != null ? -1 : hash; }
            DrawDynamic();
        }

        /* ---------- Tahta ---------- */
        void DrawBoard()
        {
            var th = Theme;
            var sq = PixelArt.Square;
            boardPool.Begin();
            boardPool.Draw(sq, new Vector2(w / 2f, -h / 2f), new Vector2(w, h), th.grout, OBoard);
            const float wt = .2f, fh = .12f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int t = tiles[y, x];
                    float s = seeds[y, x];
                    var f = feat[y, x];
                    int cx = x, cy = y;
                    Each(x, y, off =>
                    {
                        Vector2 c = Cell(cx, cy) + new Vector2(off.x, -off.y);
                        var fc = ((cx + cy) & 1) == 0 ? th.floorA : th.floorB;
                        fc = Color.Lerp(fc, th.grout, s * .25f);
                        boardPool.Draw(sq, c, new Vector2(.96f, .96f), fc, OBoard + 1);
                        if (s > .82f) boardPool.Draw(sq, c + new Vector2((s - .9f) * 3, (s - .86f) * 4), new Vector2(.1f, .06f), new Color(th.moss.r, th.moss.g, th.moss.b, .45f), OBoard + 2);
                        if (f != null && f.slide != 0)
                        {
                            float ang = f.slide == D.E ? 0 : f.slide == D.N ? 90 : f.slide == D.W ? 180 : 270;
                            Vector2 dv = new Vector2(D.DX(f.slide), -D.DY(f.slide));
                            boardPool.Draw(sq, c, new Vector2(.8f, .8f), new Color(th.moss.r, th.moss.g, th.moss.b, .12f), OBoard + 2);
                            boardPool.Draw(PixelArt.Chevron, c - dv * .15f, Vector2.one * .45f, new Color(th.moss.r, th.moss.g, th.moss.b, .55f), OBoard + 3, ang);
                            boardPool.Draw(PixelArt.Chevron, c + dv * .12f, Vector2.one * .45f, new Color(th.moss.r, th.moss.g, th.moss.b, .85f), OBoard + 3, ang);
                        }
                        if (f != null && f.rot)
                        {
                            boardPool.Draw(PixelArt.Circle, c, Vector2.one * .86f, Data.C("#3a2a10"), OBoard + 2);
                            boardPool.Draw(PixelArt.Ring, c, Vector2.one * .86f, Data.C("#ffb02a"), OBoard + 3);
                            // Açık yönleri gösteren çubuk
                            bool ns = (t & (D.N | D.S)) != 0;
                            boardPool.Draw(sq, c, ns ? new Vector2(.16f, .7f) : new Vector2(.7f, .16f), Data.C("#ffd04a"), OBoard + 4);
                        }
                        // Her karo kendi kapalı kenarlarını çizer; komşu ile çakışan duvarlar üst üste biner
                        foreach (var d in D.ALL)
                        {
                            if ((t & d) != 0) continue;
                            DrawWall(c, d, wt, fh, th);
                        }
                    });
                }
            // Dış sınırlar her zaman kapalı
            for (int x = 0; x < w; x++) { DrawWall(Cell(x, 0), D.N, wt, fh, th); DrawWall(Cell(x, h - 1), D.S, wt, fh, th); }
            for (int y = 0; y < h; y++) { DrawWall(Cell(0, y), D.W, wt, fh, th); DrawWall(Cell(w - 1, y), D.E, wt, fh, th); }
            boardPool.End();
        }

        void DrawWall(Vector2 c, int d, float wt, float fh, Theme th)
        {
            var sq = PixelArt.Square;
            if (d == D.N || d == D.S)
            {
                float ey = c.y + (d == D.N ? .5f : -.5f);
                boardPool.Draw(sq, new Vector2(c.x, ey - wt / 2 - fh / 2), new Vector2(1 + wt, fh), th.front, OWall);
                boardPool.Draw(sq, new Vector2(c.x, ey), new Vector2(1 + wt, wt), th.top, OWall + 1);
                boardPool.Draw(sq, new Vector2(c.x, ey + wt / 2 - .025f), new Vector2(1 + wt, .05f), th.hi, OWall + 2);
            }
            else
            {
                float ex = c.x + (d == D.E ? .5f : -.5f);
                boardPool.Draw(sq, new Vector2(ex, c.y - .5f - wt / 2 - fh / 2), new Vector2(wt, fh), th.front, OWall);
                boardPool.Draw(sq, new Vector2(ex, c.y), new Vector2(wt, 1 + wt), th.top, OWall + 1);
                boardPool.Draw(sq, new Vector2(ex - wt / 2 + .025f, c.y), new Vector2(.05f, 1 + wt), th.hi, OWall + 2);
            }
        }

        /* ---------- Varlıklar ve efektler ---------- */
        Vector2 EntPos(Ent e, Vector2 off) { var p = e.Pos(); return Cell(p.x + off.x, p.y + off.y); }

        void DrawDynamic()
        {
            float time = Time.time;
            var sq = PixelArt.Square;
            dynPool.Begin();

            // Kayma uyarısı: kayacak satır/sütun yanıp söner
            if (warn != null)
            {
                var col = new Color(.29f, .66f, 1f, .14f + .1f * Mathf.Sin(time * 11));
                if (warn.row) dynPool.Draw(sq, new Vector2(w / 2f, -(warn.idx + .5f)), new Vector2(w, 1), col, OItem - 1);
                else dynPool.Draw(sq, new Vector2(warn.idx + .5f, -h / 2f), new Vector2(1, h), col, OItem - 1);
            }
            // Meşaleler: üst duvarı kapalı karoların bir kısmında
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float s = seeds[y, x];
                    if (s >= .17f || (tiles[y, x] & D.N) != 0) continue;
                    int cx = x, cy = y;
                    Each(x, y, off =>
                    {
                        Vector2 c = Cell(cx, cy) + new Vector2(off.x, -off.y) + new Vector2(0, .36f);
                        float fl = .9f + .15f * Mathf.Sin(time * 13 + s * 50);
                        dynPool.Draw(PixelArt.Glow, c, Vector2.one * .9f * fl, new Color(1f, .6f, .15f, .35f), OFx - 10);
                        dynPool.Draw(sq, c + new Vector2(0, -.08f), new Vector2(.06f, .14f), Data.C("#5a3a1a"), OWall + 5);
                        dynPool.Draw(PixelArt.Circle, c + new Vector2(0, .02f), new Vector2(.12f, .16f) * fl, Data.C("#ffb02a"), OWall + 6);
                        dynPool.Draw(PixelArt.Circle, c + new Vector2(0, .01f), new Vector2(.06f, .09f) * fl, Data.C("#fff0a0"), OWall + 7);
                    });
                    if (state == State.Play && anim == null && Random.value < .02f)
                        parts.Add(new Particle { pos = new Vector2(x + .5f + Random.Range(-.05f, .05f), y + .5f - .5f), vel = new Vector2(Random.Range(-.15f, .15f), Random.Range(-.9f, -.45f)),
                            color = Random.value < .5f ? Data.C("#ffb02a") : Data.C("#ffd04a"), ttl = Random.Range(700, 1300), size = .045f, floaty = true });
                }
            // Döner koridor dönmek üzere: çevresinde dönen halka
            if (rotTimer < 900 && anim == null)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (feat[y, x] != null && feat[y, x].rot)
                            dynPool.Draw(PixelArt.Ring, Cell(x, y), Vector2.one * 1.05f, new Color(1, .69f, .16f, .5f + .4f * Mathf.Sin(time * 14)), OItem - 1, -time * 360);

            bool lampOn = P.lamp > 0;
            // Gizli bölme: yakındayken ya da fener yanarken hafifçe parıldar
            if (secret != null && (lampOn || Mathf.Abs(secret.x - P.x) + Mathf.Abs(secret.y - P.y) <= 2))
                Each(secret.x, secret.y, off => dynPool.Draw(PixelArt.Glow, EntPos(secret, off), Vector2.one * 1.3f,
                    new Color(.78f, .49f, 1f, (lampOn ? .55f : .28f) + .12f * Mathf.Sin(time * 5)), OItem));
            if (exit != null)
                Each(exit.x, exit.y, off =>
                {
                    var c = EntPos(exit, off);
                    if (hasKey) dynPool.Draw(PixelArt.Glow, c, Vector2.one * (1.4f + .1f * Mathf.Sin(time * 6)), new Color(1, .75f, .2f, .6f), OItem);
                    dynPool.Draw(PixelArt.Get(hasKey ? "door_open" : "door").normal, c, Vector2.one * .82f, Color.white, OItem + 1);
                });
            foreach (var it in items)
            {
                bool hid = IsSecret(it.x, it.y);
                if (hid && !lampOn) continue;
                var item = it;
                Each(it.x, it.y, off =>
                {
                    var c = EntPos(item, off) + new Vector2(0, .04f * Mathf.Sin(time * 3 + item.x));
                    var col = new Color(1, 1, 1, hid ? .45f : 1);
                    if (item.type == "chest") dynPool.Draw(PixelArt.Get("chest").normal, c + new Vector2(0, .1f), Vector2.one * (item.big ? 1.05f : .85f), col, OItem + 2);
                    else dynPool.Draw(PixelArt.Get(item.type).normal, c, Vector2.one * .62f, col, OItem + 2);
                    if (item.type == "gem" || item.big) dynPool.Draw(PixelArt.Glow, c, Vector2.one * .9f, new Color(.8f, .5f, 1f, .3f * col.a), OItem + 1);
                });
            }
            if (key != null)
                Each(key.x, key.y, off =>
                {
                    var c = EntPos(key, off) + new Vector2(0, .05f * Mathf.Sin(time * 4));
                    dynPool.Draw(PixelArt.Glow, c, Vector2.one * 1.1f, new Color(1, .85f, .3f, .45f), OItem + 1);
                    dynPool.Draw(PixelArt.Get("key").normal, c, Vector2.one * .7f, Color.white, OItem + 2);
                });
            foreach (var k in coins)
            {
                var coin = k;
                Each(k.x, k.y, off =>
                {
                    var c = EntPos(coin, off);
                    float spin = Mathf.Abs(Mathf.Cos(time * 3 + coin.x * .7f + coin.y));
                    float sz = coin.v > 10 ? .5f : .38f;
                    dynPool.Draw(PixelArt.Get("coin").normal, c, new Vector2(sz * Mathf.Max(.25f, spin), sz), Color.white, OItem + 3);
                });
            }
            foreach (var m in monsters) { var mm = m; Each(m.x, m.y, off => DrawMonster(mm, off, time)); }
            Each(P.x, P.y, off => DrawPlayer(off, time));

            // Büyü topları
            foreach (var b in bolts)
            {
                if (b.t >= 1) continue;
                var p = Vector2.Lerp(b.a, b.b, b.t);
                var c = new Vector2(p.x, -p.y);
                dynPool.Draw(PixelArt.Glow, c, Vector2.one * .6f, new Color(.6f, .82f, 1f, .8f), OFx + 1);
                dynPool.Draw(PixelArt.Circle, c, Vector2.one * .16f, Color.white, OFx + 2);
            }
            foreach (var q in parts)
                dynPool.Draw(sq, new Vector2(q.pos.x, -q.pos.y), Vector2.one * q.size, new Color(q.color.r, q.color.g, q.color.b, Mathf.Clamp01(q.life)), OFx + 5);

            // Çerçeve: tahta dışına taşan kayan karoları örter
            float big = 200;
            dynPool.Draw(sq, new Vector2(w / 2f, big / 2 + .15f), new Vector2(big, big), Bg, OFrame);
            dynPool.Draw(sq, new Vector2(w / 2f, -h - big / 2 - .15f), new Vector2(big, big), Bg, OFrame);
            dynPool.Draw(sq, new Vector2(-big / 2 - .15f, -h / 2f), new Vector2(big, big), Bg, OFrame);
            dynPool.Draw(sq, new Vector2(w + big / 2 + .15f, -h / 2f), new Vector2(big, big), Bg, OFrame);
            dynPool.End();
        }

        void Shadow(Vector2 c, float width) =>
            dynPool.Draw(PixelArt.Circle, c + new Vector2(0, -.32f), new Vector2(width, width * .32f), new Color(0, 0, 0, .35f), OEnt - 1);

        void DrawPlayer(Vector2 off, float time)
        {
            var c = EntPos(P, off);
            int order = OEnt + Mathf.RoundToInt(P.Pos().y * 10) + 5;
            Vector2 fv = new Vector2(D.DX(P.face), -D.DY(P.face));
            Vector2 body = c + fv * P.lunge * .16f;
            float lift = P.jumping && P.t < 1 ? Mathf.Sin(Mathf.PI * P.t) * .6f : 0;
            Shadow(c, .7f * (1 - lift / 1.6f));
            body.y += lift;
            if (Raging) dynPool.Draw(PixelArt.Glow, body, Vector2.one * 1.4f, new Color(1, .24f, .16f, .35f + .15f * Mathf.Sin(time * 11)), order - 2);
            if (P.spinFx > 0) dynPool.Draw(PixelArt.Ring, body, Vector2.one * 1.35f, new Color(1, 1, 1, P.spinFx), order + 3, (1 - P.spinFx) * 720);
            // Yürürken bacak kareleri, dururken hafif nefes
            int frame = Moving ? new[] { 0, 1, 0, 2 }[(int)(time / .12f) % 4] : 0;
            float breathe = !Moving && (int)(time / .6f) % 2 == 1 ? -1 / 24f : 0;
            bool back = P.face == D.N;
            var set = PixelArt.Heroes.TryGetValue(R.hero, out var hs) ? hs : PixelArt.Heroes["knight"];
            var spr = (back ? set.back : set.front)[frame];
            if (back) DrawWeapon(body, order - 1);
            dynPool.Draw(P.hurt > .6f ? spr.white : spr.normal, body + new Vector2(0, .14f + breathe), Vector2.one * 1.1f, Color.white, order);
            if (!back) DrawWeapon(body, order + 1);
            if (P.shield > 0)
                dynPool.Draw(PixelArt.Ring, body + new Vector2(0, .1f), Vector2.one * (1.25f + .05f * Mathf.Sin(time * 8)), new Color(.48f, .7f, 1f, P.shield < 1000 && (int)(time * 8) % 2 == 0 ? .3f : .8f), order + 4);
        }

        // Elde tutulan silah: saldırırken kavis çizerek savrulur
        void DrawWeapon(Vector2 body, int order)
        {
            string wp = Cls.weapon;
            int sd = P.side;
            Vector2 hand = body + new Vector2(sd * .3f, -.1f);
            float baseAng = sd > 0 ? -35 : 35;
            float swing = P.slash * 110 * (sd > 0 ? -1 : 1);
            if (P.face == D.N) baseAng += sd > 0 ? 60 : -60;
            float ang = 90 + baseAng + swing;
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            float len = wp == "dagger" ? .3f : wp == "staff" ? .62f : wp == "axe" ? .5f : .5f;
            var col = WeaponColor;
            var handle = Data.C("#5a3a1a");
            if (wp == "staff")
            {
                dynPool.Draw(PixelArt.Square, hand + dir * len * .35f, new Vector2(.06f, len), handle, order, ang - 90);
                dynPool.Draw(PixelArt.Glow, hand + dir * len * .85f, Vector2.one * .4f, new Color(col.r, col.g, col.b, .6f), order);
                dynPool.Draw(PixelArt.Circle, hand + dir * len * .85f, Vector2.one * .14f, col, order + 1);
                return;
            }
            dynPool.Draw(PixelArt.Square, hand + dir * .05f, new Vector2(.06f, .14f), handle, order, ang - 90);
            if (wp == "axe")
            {
                dynPool.Draw(PixelArt.Square, hand + dir * len * .5f, new Vector2(.06f, len), handle, order, ang - 90);
                dynPool.Draw(PixelArt.Square, hand + dir * len * .85f + new Vector2(dir.y, -dir.x) * .08f * sd, new Vector2(.2f, .16f), col, order + 1, ang - 90);
            }
            else
            {
                dynPool.Draw(PixelArt.Square, hand + dir * .13f, new Vector2(.2f, .05f), Data.C("#8a6a24"), order + 1, ang - 90);
                dynPool.Draw(PixelArt.Square, hand + dir * (.13f + len / 2), new Vector2(.08f, len), col, order, ang - 90);
            }
            if (P.slash > .1f)
                dynPool.Draw(PixelArt.Ring, body + new Vector2(D.DX(P.face), -D.DY(P.face)) * .35f, Vector2.one * .9f, new Color(1, 1, 1, P.slash * .6f), order + 2);
        }

        void DrawMonster(Monster m, Vector2 off, float time)
        {
            var c = EntPos(m, off);
            int order = OEnt + Mathf.RoundToInt(m.Pos().y * 10);
            Vector2 fv = new Vector2(D.DX(m.face), -D.DY(m.face));
            float bob = m.type == "bat" || m.type == "ghost" ? .08f * Mathf.Sin(time * 6 + m.seed) + .12f : .03f * Mathf.Sin(time * 4 + m.seed);
            Shadow(c, .65f * m.size);
            var spr = PixelArt.Get(m.type);
            var body = c + fv * m.lunge * .18f + new Vector2(0, .12f * m.size + bob);
            if (m.type == "boss") dynPool.Draw(PixelArt.Glow, body, Vector2.one * 2f, new Color(1, .2f, .1f, .25f), order - 1);
            var tint = new Color(1, 1, 1, m.phase ? .78f : 1);
            dynPool.Draw(m.hurt > .5f ? spr.white : spr.normal, body, Vector2.one * m.size, tint, order, 0, m.face == D.W);
            if (m.key) dynPool.Draw(PixelArt.Get("key").normal, body + new Vector2(0, .75f * m.size), Vector2.one * .45f, Color.white, order + 2);
            if (m.hp < m.maxHp)
            {
                var bp = c + new Vector2(0, .62f * m.size + .2f);
                float k = Mathf.Clamp01((float)m.hp / m.maxHp);
                dynPool.Draw(PixelArt.Square, bp, new Vector2(.62f, .09f), Data.C("#1a0c12"), order + 1);
                dynPool.Draw(PixelArt.Square, bp - new Vector2(.3f * (1 - k), 0), new Vector2(.58f * k, .05f), Data.C("#e0413f"), order + 2);
            }
        }
    }
}
