using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>
    /// M2: dựng màn chơi từ BuzzyLevel + BuzzyLayoutConfig + BuzzyAssets, nối luật lõi (BusGame) với chuyển động.
    /// Chuyển động chạy bằng Tick(dt) thuần (Update gọi Tick) để bộ chụp trong Editor bước thời gian xác định.
    /// </summary>
    public partial class BuzzyGameView : MonoBehaviour
    {
        // ----- cấu hình + trạng thái -----
        BuzzyLayoutConfig _cfg; BuzzyAssets _a; BuzzyLevel _lv; Camera _cam; Vector2 _canvasSize;
        public BusGame Game { get; private set; }
        public float Time { get; private set; }
        public float TimeScale = 1f;
        public bool Won { get; private set; }
        public float WonAt { get; private set; }
        public int TapCount { get; private set; }
        /// <summary>M6: số level người chơi thấy (thứ tự level trong phạm vi); 0 = dùng index gốc của level.</summary>
        public int DisplayLevel;
        public Action<string> Log;
        public Action Vibrate;

        float _cellX0;          // x thế giới của cột 0
        float _boardZ0;         // z thế giới của hàng 0 (tâm bảng cố định, V33)
        class Pass { public Transform tr; public PixelData d; public Vector3 home; public float appearAt; public bool flying; public bool hidden; public MeshRenderer mr; public bool loose; public float phase; }
        class Flight { public Pass p; public CarV car; public float start, dur; public Vector3 from; public float phase, side; public float tapAt; }
        class Move { public Vector3[] pts; public float[] cum; public float dur, t; public Quaternion endRot; public bool useEndRot; public float delay; public Action done; public bool keepRot; public bool reverse; public int ease; public float[] prof; public bool bank; public CarV waitFor; }   // waitFor (B10): chưa đếm delay khi xe này còn chờ làn   // ease: 0 mặc định, 1 phanh, 2 phóng; prof: bảng quãng đường theo thời gian (tăng tốc / phanh cuối); bank: nghiêng khi cua
        class CarV
        {
            public Car car; public Transform tr; public TextMesh[] texts; public Move move; public bool leaving; public int col; public Vector3 slotPos; public GameObject lockObj; public MeshRenderer mr;
            // M7 feel: nhún (J2/J3), số bật (J4), đầy lắc (J5: -1 chưa, -2 xong), cỡ gốc
            public float bounceAt = -1f, bounceAmp, numPopAt = -1f, fullAt = -1f, baseScale, lastLift;   // lastLift: độ nhấc Y đang áp (J2 đợt 3) để gỡ trước khi áp mới
            public bool laneWait, onLane;   // B10: đang chờ làn trên trống / đang chiếm làn trên (lùi ra, đứng, phóng đi)
            // góp ý 06: chúi / ngửa mũi khi phanh, phóng (gốc _leavePivotBrakePitchAngle); lần nhảy lên xe gần nhất (lên lần lượt)
            public readonly List<Pitch> pitches = new List<Pitch>(); public Quaternion lastPitch = Quaternion.identity; public float lastBoardAt = -10f;
            public Vector3 prevDir; public float roll;   // góp ý 07: nghiêng khi cua (gốc _travelMaxBankAngle 18)
            public float lastBounceAt = -10f;
            public Transform shadow;   // V14 (góp ý 10): bóng đổ mềm, luôn nằm phẳng, lệch về phải-dưới
        }
        class Pop { public Transform tr; public float at, dur; public Vector3 scale; public float drop, dropDur, overshoot, bounce, startScale; public Vector3 basePos; }   // góp ý 16 (J7): rào rơi (drop, dropDur) → nở quá (overshoot) + nẩy 1 lần (bounce) → ổn định ở dur; drop 0 = bật cỡ như cũ
        class Pitch { public float at, amp, dur; }

        readonly Dictionary<int, Pass> _pass = new Dictionary<int, Pass>();
        readonly List<Flight> _flights = new List<Flight>();
        readonly List<CarV> _cars = new List<CarV>();
        readonly List<CarV>[] _queue = { new List<CarV>(), new List<CarV>(), new List<CarV>() };
        readonly CarV[] _slotCar = new CarV[3];
        readonly List<Pop> _pops = new List<Pop>();
        Material _vehMat0; Transform _root; Canvas _canvas; RectTransform[] _fullBubbles = new RectTransform[3]; Transform[] _fullSigns = new Transform[3];
        float _inputOpenAt;
        readonly GameObject[] _pMark = new GameObject[3]; float _baseOrtho; RectTransform _uiRoot; Image _haze; System.Random _rnd = new System.Random(3);

        public Vector3 SlotPos(int i) { return new Vector3(_cfg.slotX[i], 0f, _cfg.slotZ); }
        Vector3 QueuePos(int col, int k) { return new Vector3(_cfg.queueX[col], 0f, _cfg.queueZ + (k == 0 ? 0f : 6f + (k - 1) * 4.5f)); }
        Vector3 PixelPos(PixelData d) { return new Vector3(_cellX0 + d.X * _cfg.cellPitchX, 0f, _boardZ0 + d.Y * _cfg.cellPitch); }
        Quaternion SlotRot { get { return Quaternion.Euler(0f, _cfg.slotYaw, 0f); } }

        public void Init(BuzzyLevel lv, BuzzyLayoutConfig cfg, BuzzyAssets assets, Camera cam, Vector2Int renderSize)
        {
            _lv = lv; _cfg = cfg; _a = assets; _cam = cam; _canvasSize = new Vector2(renderSize.x, renderSize.y);
            _root = new GameObject("BuzzyWorld").transform; _root.SetParent(transform, false);
            _cellX0 = cfg.boardCenterX - (lv.width * 0.5f) * cfg.cellPitchX; _boardZ0 = cfg.BoardZ0(lv.height);
            BuildEnvironment();
            // luật lõi: tọa độ ô bến theo lưới để tính "gần xe nhất"
            var slotGrid = new double[3][];
            for (int i = 0; i < 3; i++) slotGrid[i] = new double[] { (cfg.slotX[i] - _cellX0) / cfg.cellPitchX, (cfg.slotZ - _boardZ0) / cfg.cellPitch };
            Game = new BusGame(lv.ToCore(), slotGrid, cfg.ToMechanicRules());
            SubscribeMechanics();
            Game.PixelArrived += OnPixelArrived;
            Game.CarEntered += OnCarEntered;
            Game.CarDeparted += OnCarDeparted;
            Game.BarrierPlaced += OnBarrier;
            BuildBoard();
            BuildMechanics();
            BuildCars();
            BuildUi();
            _inputOpenAt = cfg.inputDelay;
            InitBoosters();
            ApplyAudio();   // M7: cài đặt Sound / Music
            StartIntro();
        }

        // ---------------------------------------------------------------- dựng cảnh
        Material Mat(string shader, Color c) { var m = new Material(Shader.Find(shader)); m.color = c; return m; }

        GameObject Quad(string name, Color c, float cx, float z0, float z1, float halfW, float y)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(_root, false);
            q.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            q.transform.localPosition = new Vector3(cx, y, (z0 + z1) * 0.5f);
            q.transform.localScale = new Vector3(halfW * 2f, z1 - z0, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", c);
            return q;
        }

        void BuildEnvironment()
        {
            // nền chuyển sắc
            var tex = new Texture2D(1, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) tex.SetPixel(0, 63 - i, Color.Lerp(_cfg.backgroundTop, _cfg.backgroundBottom, i / 63f));
            tex.Apply();
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(bg.GetComponent<Collider>());
            bg.name = "BackgroundGradient"; bg.transform.SetParent(_cam.transform, false);
            var m = new Material(Shader.Find("Unlit/Texture")); m.mainTexture = tex; bg.GetComponent<MeshRenderer>().sharedMaterial = m;
            float dist = 150f; var bs = _cfg.BackdropSize(_cam, dist);
            bg.transform.localPosition = new Vector3(0, 0, dist); bg.transform.localScale = new Vector3(bs.x, bs.y, 1f);
            // đường + vạch đứt
            Quad("Road", _cfg.roadColor, 0f, _cfg.roadNearZ, _cfg.roadFarZ, 40f, 0f);
            for (float x = -14f; x <= 14f; x += _cfg.dashSpacing) Quad("Dash", _cfg.dashColor, x, _cfg.dashZ - _cfg.dashHalfWidth, _cfg.dashZ + _cfg.dashHalfWidth, _cfg.dashHalfLength, 0.03f);
            for (int i = 0; i < 3; i++) BuildPMark(i);
        }

        // V17: khung đứt nét + chữ P ở ô bến (chữ P chỉ hiện khi ô trống)
        void BuildPMark(int slot)
        {
            var root = new GameObject("PMark" + slot); root.transform.SetParent(_root, false);
            root.transform.localPosition = SlotPos(slot) + new Vector3(0f, 0.02f, 0f); root.transform.localRotation = SlotRot;
            var mat = Mat("Custom/BuzzyEnvironment", _cfg.pMarkColor);
            float w = _cfg.pMarkSize.x, l = _cfg.pMarkSize.y, d = _cfg.pMarkDash, t = _cfg.pMarkLine;
            Action<Vector3, Vector3> seg = (c, sz) =>
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, false); q.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                q.transform.localPosition = c; q.transform.localScale = new Vector3(sz.x, sz.z, 1f); q.GetComponent<MeshRenderer>().sharedMaterial = mat;
            };
            for (float z = -l * 0.5f + d * 0.5f; z <= l * 0.5f; z += d * 2f) { seg(new Vector3(-w * 0.5f, 0f, z), new Vector3(t, 0f, d)); seg(new Vector3(w * 0.5f, 0f, z), new Vector3(t, 0f, d)); }
            for (float x = -w * 0.5f + d * 0.5f; x <= w * 0.5f; x += d * 2f) { seg(new Vector3(x, 0f, -l * 0.5f), new Vector3(d, 0f, t)); seg(new Vector3(x, 0f, l * 0.5f), new Vector3(d, 0f, t)); }
            var pgo = new GameObject("P"); pgo.transform.SetParent(root.transform, false);
            pgo.transform.localPosition = new Vector3(0f, 0.03f, 0f); pgo.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            var tm = pgo.AddComponent<TextMesh>(); tm.text = "P"; tm.font = _a.font; tm.fontSize = 64; tm.characterSize = _cfg.pLetterSize * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = _cfg.pMarkColor;
            var m2 = new Material(_a.font.material); m2.renderQueue = 2450; pgo.GetComponent<MeshRenderer>().sharedMaterial = m2;
            _pMark[slot] = pgo; pgo.SetActive(false);
        }

        // C3/C4: vào màn: khung phóng to từ giữa + màn đỏ tan dần
        void StartIntro()
        {
            _baseOrtho = _cam.orthographicSize;
            if (!_cam.orthographic || _cfg.introZoomDuration <= 0f) return;
            _cam.orthographicSize = _baseOrtho * _cfg.introZoom;
            if (_uiRoot != null) _uiRoot.localScale = Vector3.one / _cfg.introZoom;
            var go = new GameObject("Haze", typeof(RectTransform), typeof(Image)); go.transform.SetParent(_canvas.transform, false);
            _haze = go.GetComponent<Image>(); _haze.color = _cfg.introHazeColor; _haze.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void TickIntro()
        {
            if (_baseOrtho <= 0f || _cfg.introZoomDuration <= 0f) return;
            float u = Mathf.Clamp01(Time / _cfg.introZoomDuration), e = 1f - (1f - u) * (1f - u);
            float k = Mathf.Lerp(_cfg.introZoom, 1f, e);
            _cam.orthographicSize = _baseOrtho * k;
            if (_uiRoot != null) _uiRoot.localScale = Vector3.one / k;
            if (_haze != null)
            {
                float a = _cfg.introHazeColor.a * (1f - Mathf.Clamp01(Time / _cfg.introHazeDuration));
                _haze.color = new Color(_cfg.introHazeColor.r, _cfg.introHazeColor.g, _cfg.introHazeColor.b, a);
                if (a <= 0f) { Kill(_haze.gameObject); _haze = null; }
            }
        }

        readonly Dictionary<int, Material> _blockMats = new Dictionary<int, Material>();
        float _buildSeed;

        void BuildBoard()
        {
            var b = Game.Board;
            for (int x = 0; x < b.W; x++) for (int y = 0; y < b.H; y++)
            {
                int c = b.ColorAt(x, y); if (c == Board.Empty) continue;
                // dựng từ dưới-trái lan chéo lên phải (C5)
                float diag = ((float)x / Mathf.Max(1, _lv.width)) * 0.55f + ((float)y / Mathf.Max(1, _lv.height)) * 0.45f;
                _buildSeed += 0.37f;
                CreatePass(new PixelData(x, y, c), diag * _cfg.appearSpreadDuration.x + 0.04f * Mathf.Abs(Mathf.Sin(_buildSeed)));
            }
            MarkLooseClusters();
        }

        // V23 [thiết kế]: cụm không dính ảnh chính (thành phần liên thông nhỏ hơn) đứng tách ra: dịch vào tâm + xa camera, nhấp nhô nhẹ
        void MarkLooseClusters()
        {
            var b = Game.Board; var comp = new int[b.W, b.H]; int n = 0; var sizes = new List<int>();
            var stack = new Stack<int>();
            for (int x = 0; x < b.W; x++) for (int y = 0; y < b.H; y++)
            {
                if (b.ColorAt(x, y) == Board.Empty || comp[x, y] != 0) continue;
                n++; int size = 0; stack.Push(x * 1000 + y); comp[x, y] = n;
                while (stack.Count > 0)
                {
                    int k = stack.Pop(), cx = k / 1000, cy = k % 1000; size++;
                    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + dx[d], ny = cy + dy[d];
                        if (!b.InBounds(nx, ny) || b.ColorAt(nx, ny) == Board.Empty || comp[nx, ny] != 0) continue;
                        comp[nx, ny] = n; stack.Push(nx * 1000 + ny);
                    }
                }
                sizes.Add(size);
            }
            if (n <= 1) return;
            int main = 1; for (int i = 1; i < sizes.Count; i++) if (sizes[i] > sizes[main - 1]) main = i + 1;
            float cx0 = _cfg.boardCenterX;
            foreach (var kv in _pass)
            {
                var p = kv.Value; int c = comp[p.d.X, p.d.Y];
                if (c == main || b.IsFrozen(p.d.X, p.d.Y)) continue;
                float dir = p.home.x < cx0 ? 1f : -1f;
                p.home += new Vector3(dir * _cfg.looseInward * _cfg.cellPitchX, 0f, _cfg.looseLift);
                p.loose = true; p.phase = c * 1.7f;
            }
        }

        Material PassMat(int color)
        {
            Material mat;
            if (!_blockMats.TryGetValue(color, out mat)) { mat = Mat("Custom/BuzzyPassenger", BuzzyPalette.Get(color)); _blockMats[color] = mat; }
            return mat;
        }

        Pass CreatePass(PixelData d, float appearAt)
        {
            var go = new GameObject("P");
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.blockMesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = PassMat(d.Color);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.7f, 0f); bc.size = new Vector3(1.2f, 1.4f, 1.0f);
            var ps = new Pass { tr = go.transform, d = d, home = PixelPos(d), appearAt = appearAt, mr = mr };
            _pass[d.X * 1000 + d.Y] = ps;
            SetPassScale(ps, 0f);
            return ps;
        }

        Vector3 BlockScale { get { return new Vector3(_cfg.blockScale * _cfg.blockWidthScale, _cfg.blockScale * _cfg.blockHeightScale, _cfg.blockScale); } }

        void SetPassScale(Pass p, float s)
        {
            p.tr.localScale = BlockScale * s;
            p.tr.localPosition = p.home + new Vector3(0f, -1.2f * (1f - Mathf.Clamp01(s)), 0f);
            p.tr.localEulerAngles = new Vector3(0, 180f, 0);
        }

        CarV MakeCar(Car car, Vector3 pos, Quaternion rot, float scale)
        {
            var mat = Mat("Custom/BuzzyVehicle", BuzzyPalette.Get(car.Color)); mat.SetTexture("_BaseTex", _a.busTexture);
            var go = new GameObject("Car" + car.Id);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.busMesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = Vector3.one * scale;
            var cv = new CarV { car = car, tr = go.transform, mr = go.GetComponent<MeshRenderer>(), baseScale = scale };
            cv.shadow = MakeShadow(go.transform);
            var bcol = go.AddComponent<BoxCollider>(); bcol.center = new Vector3(0f, 0.8f, 0f); bcol.size = new Vector3(1.7f, 1.6f, 2.8f);
            cv.texts = AddRoofNumber(go.transform, car.Remaining);
            if (car.Locked) AddLockOverlay(cv);
            _cars.Add(cv);
            return cv;
        }

        /// <summary>V14 (góp ý 10): quad bóng mềm (Shadow.png) con của xe; vị trí / hướng cập nhật mỗi khung trong TickFeel để luôn phẳng trên mặt đường.</summary>
        Transform MakeShadow(Transform carTr)
        {
            if (_cfg.carShadowAlpha <= 0f) return null;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "Shadow"; q.transform.SetParent(carTr, false);
            // Shadow.png gốc là atlas nhiều hình bóng (không dùng được nguyên tấm) → tự sinh bóng chữ nhật bo góc viền mờ; màu tối nhân alpha (3c đo mẫu: tối hơn đường ~31–35 %)
            var m = new Material(Shader.Find("Sprites/Default")); m.mainTexture = SoftShadowTex(); m.color = new Color(_cfg.carShadowColor.r, _cfg.carShadowColor.g, _cfg.carShadowColor.b, _cfg.carShadowAlpha); m.renderQueue = 2900;
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
            return q.transform;
        }

        Texture2D _softShadowTex;
        /// <summary>Bóng mềm: chữ nhật bo góc, alpha 1 ở lõi giảm mượt về 0 ở mép (carShadowSoft = phần mép mờ).</summary>
        Texture2D SoftShadowTex()
        {
            if (_softShadowTex != null) return _softShadowTex;
            int n = 96; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float soft = Mathf.Clamp(_cfg.carShadowSoft, 0.05f, 0.5f), r = 0.25f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;   // -1..1
                float qx = Mathf.Max(Mathf.Abs(u) - (1f - r - soft), 0f), qy = Mathf.Max(Mathf.Abs(v) - (1f - r - soft), 0f);
                float dist = Mathf.Sqrt(qx * qx + qy * qy) - r;   // khoảng cách tới chữ nhật bo góc
                float a = 1f - Mathf.Clamp01(dist / soft); a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply(); _softShadowTex = t; return t;
        }

        TextMesh[] AddRoofNumber(Transform carTr, int n)
        {
            var list = new List<TextMesh>();
            var root = new GameObject("Seats"); root.transform.SetParent(carTr, false);
            root.transform.localPosition = new Vector3(0f, 1.56f, 0.05f); root.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            Vector2[] offs = { new Vector2(0.024f, 0f), new Vector2(-0.024f, 0f), new Vector2(0f, 0.024f), new Vector2(0f, -0.024f) };
            foreach (var o in offs)
            {
                var sh = new GameObject("Outline"); sh.transform.SetParent(root.transform, false);
                sh.transform.localPosition = new Vector3(o.x, o.y, -0.02f);
                var t2 = MakeText(sh, n, new Color(0.1f, 0.1f, 0.15f, 1f), 2990); list.Add(t2);
            }
            list.Add(MakeText(root, n, Color.white, 3010));
            return list.ToArray();
        }

        TextMesh MakeText(GameObject go, int n, Color c, int queue)
        {
            var tm = go.AddComponent<TextMesh>();
            tm.text = n.ToString(); tm.font = _a.font; tm.fontSize = 64; tm.characterSize = _cfg.roofNumberSize * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
            var m = new Material(_a.font.material); m.renderQueue = queue;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return tm;
        }

        void BuildCars()
        {
            // xe ở ô bến: Core đã Refill lúc dựng; xuất hiện từ phía trên (hầm), lăn xuống ô (C1)
            for (int i = 0; i < 3; i++)
            {
                var car = Game.Slots[i];
                if (car == null) continue;
                var cv = MakeCar(car, SlotPos(i) + new Vector3(0f, 0f, _cfg.enterFromZ), Quaternion.identity, _cfg.busScale);
                cv.col = i; cv.slotPos = SlotPos(i); _slotCar[i] = cv;
                StartTravel(cv, SlotPos(i), SlotRot, i * _cfg.enterStagger, true);   // góp ý 07: có tăng tốc / phanh / nghiêng
            }
            for (int i = 0; i < 3; i++)
            {
                int k = 0;
                foreach (var car in Game.Queues[i])
                {
                    var cv = MakeCar(car, QueuePos(i, k) + new Vector3(0f, 0f, k == 0 ? _cfg.enterFromZ : 0f), Quaternion.identity, _cfg.queueBusScale * _cfg.busScale);
                    cv.col = i; _queue[i].Add(cv);
                    if (k == 0) StartTravel(cv, QueuePos(i, 0), Quaternion.identity, _cfg.queueEnterDelay + i * _cfg.enterStagger, false);
                    k++;
                }
            }
        }

        void StartMove(CarV cv, Vector3[] pts, float dur, Quaternion endRot, bool useEnd, float delay, Action done = null)
        {
            var mv = new Move { pts = pts, dur = Mathf.Max(0.01f, dur), t = 0f, endRot = endRot, useEndRot = useEnd, delay = delay, done = done };
            mv.cum = new float[pts.Length]; for (int i = 1; i < pts.Length; i++) mv.cum[i] = mv.cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            cv.move = mv; cv.prevDir = Vector3.zero;
        }

        /// <summary>Góp ý 07 (gốc _travelAcceleration 30, _travelMaxSpeed 20, _travelArrivalBrakeDistance 2.5, _travelMinArrivalSpeed 0.6): xe chạy theo hồ sơ tốc độ thật:
        /// tăng tốc đều, giữ tối đa, phanh về tốc độ tới nơi trong quãng phanh. Trả thời lượng + bảng quãng đường (33 mốc) để StepCar nội suy.</summary>
        float TravelProfile(float length, out float[] prof)
        {
            float a = Mathf.Max(1f, _cfg.travelAcceleration), vmax = Mathf.Max(0.5f, _cfg.travelMaxSpeed), vmin = Mathf.Max(0.1f, _cfg.travelMinArrivalSpeed), brake = Mathf.Max(0.1f, _cfg.travelArrivalBrakeDistance);
            float decel = Mathf.Max(1f, (vmax * vmax - vmin * vmin) / (2f * brake));
            var ts = new List<float> { 0f }; var ss = new List<float> { 0f }; float v = 0f, s = 0f, t = 0f, dt = 0.005f; int guard = 20000;
            while (s < length && guard-- > 0)
            {
                float allowed = Mathf.Sqrt(Mathf.Max(0f, vmin * vmin + 2f * decel * (length - s)));
                v = Mathf.Min(v + a * dt, vmax, allowed); s += v * dt; t += dt; ts.Add(t); ss.Add(Mathf.Min(s, length));
            }
            prof = new float[33];
            for (int i = 0; i < 33; i++)
            {
                float ti = t * i / 32f; int k = 0; while (k < ts.Count - 2 && ts[k + 1] < ti) k++;
                float u = ts[k + 1] > ts[k] ? Mathf.InverseLerp(ts[k], ts[k + 1], ti) : 0f; prof[i] = Mathf.Lerp(ss[k], ss[k + 1], u) / Mathf.Max(0.001f, length);
            }
            prof[32] = 1f; return Mathf.Max(0.05f, t);
        }

        /// <summary>Đường cong từ vị trí hiện tại (đang hướng -z) tới điểm đỗ theo hướng mũi lúc đỗ (Bezier bậc 3), để xe cua mượt và có nghiêng.</summary>
        Vector3[] CurveTo(Vector3 from, Vector3 to, Quaternion endRot, int n = 14)
        {
            var nose = endRot * Vector3.back; float L = Vector3.Distance(from, to) * _cfg.travelCurveHandle;
            var p1 = from + Vector3.back * L; var p2 = to - nose * L; var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) { float u = i / (float)n, w = 1f - u; pts[i] = w * w * w * from + 3f * w * w * u * p1 + 3f * w * u * u * p2 + u * u * u * to; }
            return pts;
        }

        /// <summary>Xe chạy tới điểm đỗ theo hồ sơ tốc độ + đường cong + nghiêng; chúi mũi khi phanh cuối (gốc _travelWeightShift, _arrivalSettle).</summary>
        void StartTravel(CarV cv, Vector3 to, Quaternion endRot, float delay, bool curve)
        {
            var pts = curve ? CurveTo(cv.tr.localPosition, to, endRot) : new[] { cv.tr.localPosition, to };
            float len = 0f; for (int i = 1; i < pts.Length; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            float[] prof; float dur = TravelProfile(len, out prof);
            StartMove(cv, pts, dur, endRot, true, delay); cv.move.prof = prof; cv.move.bank = curve;
            cv.pitches.Add(new Pitch { at = Time + delay, amp = -_cfg.travelAccelPitch, dur = 0.4f });                      // tăng tốc: ngửa nhẹ
            cv.pitches.Add(new Pitch { at = Time + delay + dur - 0.35f, amp = _cfg.travelBrakePitch, dur = 0.5f });         // phanh tới nơi: chúi rồi nhún
        }

        // ---------------------------------------------------------------- UI (theo % khung)
        void BuildUi()
        {
            var cv = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler)); cv.transform.SetParent(_root, false);
            _canvas = cv.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceCamera; _canvas.worldCamera = _cam; _canvas.planeDistance = 1f;
            var sc = cv.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = _canvasSize;
            var ur = new GameObject("UiRoot", typeof(RectTransform)); ur.transform.SetParent(cv.transform, false);
            _uiRoot = ur.GetComponent<RectTransform>(); _uiRoot.anchorMin = Vector2.zero; _uiRoot.anchorMax = Vector2.one; _uiRoot.offsetMin = _uiRoot.offsetMax = Vector2.zero;
            var ct = _uiRoot;
            Img("Tray", _a.tunnel, Color.white, ct, new Vector2(0.5f, _cfg.trayVisibleHeight * 0.5f - 0.11f + _cfg.trayShift), 1.06f);
            float barTop = _lv.tag > 0 ? _cfg.hardBottomBarTop : _cfg.bottomBarTop;
            Img("BottomBar", null, _lv.tag > 0 ? _cfg.hardBottomBarColor : _cfg.bottomBarColor, ct, new Vector2(0.5f, (1f + barTop) * 0.5f), 1f, (1f - barTop) * _canvasSize.y / _canvasSize.x);
            // U8: level khó / siêu khó dùng badge đầu lâu + thanh đáy đỏ
            var badgeSprite = _lv.tag == 1 ? _a.levelBarHard : _lv.tag == 2 ? _a.levelBarSuper : _a.levelBar;
            var badge = Img("LvBadge", badgeSprite, Color.white, ct, _lv.tag > 0 ? _cfg.badgeCenter + new Vector2(0f, _cfg.hardBadgeDropY) : _cfg.badgeCenter, _lv.tag > 0 ? _cfg.badgeWidth * _cfg.hardBadgeScale : _cfg.badgeWidth);
            Label("Lv." + (DisplayLevel > 0 ? DisplayLevel : _lv.index), badge, new Vector2(badge.sizeDelta.x * 0.5f, -badge.sizeDelta.y * (_lv.tag > 0 ? 0.6f : 0.38f)), (int)(52 * _canvasSize.x / 1080f), _lv.tag > 0 ? _cfg.hardBadgeTextColor : new Color32(0x4A, 0x45, 0x9A, 255));
            BuildBoosterBar(ct);
            BuildSpeedButton(ct);
            BuildDevPanel(ct);   // T1 (M7): chỉ khi DevPanel
            Img("Settings", _a.settings, Color.white, ct, _cfg.settingsCenter, _cfg.settingsSize);
            for (int i = 0; i < 3; i++)
            {
                var b = Img("Full" + i, _a.knob, new Color(0.72f, 0.75f, 0.95f, 1f), ct, _cfg.fullBubbleCenter, _cfg.fullBubbleWidth, 1f);
                Label("Full", b, new Vector2(b.sizeDelta.x * 0.5f, -b.sizeDelta.y * 0.45f), (int)(40 * _canvasSize.x / 1080f), new Color32(0x4A, 0x45, 0x9A, 255));
                b.gameObject.SetActive(false); _fullBubbles[i] = b;
                // góp ý 14 (V16, sbs_full_zoom.jpg): bong bóng là BIỂN 3D Sign_Full.obj (tròn 2 lớp, đuôi giọt chạm mặt đường) ở góc trước-trái xe; ảnh UI chỉ còn giữ chữ "Full"
                if (_a.fullSignMesh != null)
                {
                    b.GetComponent<UnityEngine.UI.Image>().enabled = false;
                    var sg = new GameObject("FullSign" + i); sg.transform.SetParent(_root, false);
                    sg.AddComponent<MeshFilter>().sharedMesh = _a.fullSignMesh; sg.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", _cfg.fullSignColor);   // UV mesh trỏ vào màu đường trong atlas → tô phẳng màu mẫu
                    sg.transform.localScale = Vector3.one * _cfg.fullSignScale; sg.SetActive(false); _fullSigns[i] = sg.transform;
                    // lớp 2 (viền sáng): bản sao to hơn fullSignRimScale, tâm đĩa trùng, lùi 0,03 về phía xa camera (local +z = hướng nhìn)
                    var rim = new GameObject("Rim"); rim.transform.SetParent(sg.transform, false);
                    rim.AddComponent<MeshFilter>().sharedMesh = _a.fullSignMesh; rim.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", _cfg.fullSignRimColor);
                    rim.transform.localScale = Vector3.one * _cfg.fullSignRimScale; rim.transform.localPosition = new Vector3(0f, _cfg.fullSignDiscY * (1f - _cfg.fullSignRimScale), 0.03f);
                }
            }
            Canvas.ForceUpdateCanvases();
        }

        RectTransform Img(string name, Sprite sprite, Color color, Transform parent, Vector2 pct, float widthPct, float? aspect = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.sprite = sprite; im.color = color; im.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            float w = widthPct * _canvasSize.x; float asp = aspect ?? (sprite != null ? sprite.rect.height / sprite.rect.width : 1f);
            rt.sizeDelta = new Vector2(w, w * asp); rt.anchoredPosition = new Vector2(pct.x * _canvasSize.x, -pct.y * _canvasSize.y);
            return rt;
        }

        Text Label(string text, Transform parent, Vector2 anchored, int size, Color c)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = text; t.font = _a.font; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.sizeDelta = new Vector2(260, 90); rt.anchoredPosition = anchored;
            return t;
        }

        // ---------------------------------------------------------------- sự kiện luật lõi
        CarV ViewOf(Car c) { foreach (var v in _cars) if (v.car == c) return v; return null; }

        void OnPixelArrived(Car car, PixelData p)
        {
            var v = ViewOf(car);
            if (v != null) foreach (var t in v.texts) t.text = Mathf.Max(0, car.Remaining).ToString();
            FeelArrive(v);   // M7: pop + nhún + số bật
        }

        void OnCarDeparted(Car car, int slot)
        {
            var cv = ViewOf(car); if (cv == null) return;
            cv.leaving = true; _slotCar[slot] = null;
            FeelDepart(cv);   // M7
            var s = SlotPos(slot);
            // C13 (góp ý 05): xe LÙI vòng cung, đít quét sang phải tới khi thẳng hàng với làn trên (mũi quay sang trái), dừng ngắn rồi PHÓNG sang trái, thoát mép trái; vệt bánh lúc phóng.
            float R = Mathf.Max(0.8f, _cfg.leaveLaneZ - s.z); var center = new Vector3(s.x + R, 0f, s.z);
            var arc = new List<Vector3>(); int segs = 8;
            for (int k = 0; k <= segs; k++) { float th = Mathf.PI - k * (Mathf.PI * 0.5f) / segs; arc.Add(center + new Vector3(R * Mathf.Cos(th), 0f, R * Mathf.Sin(th))); }
            var pts = arc.ToArray(); float len = 0f; for (int i = 1; i < pts.Length; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            var laneEnd = pts[pts.Length - 1];
            Action gone = () => { _cars.Remove(cv); Kill(cv.tr.gameObject); };
            Action driveOff = () =>
            {
                // góp ý 06: phanh gấp (ngửa mũi), đứng leaveLaunchPause, rồi phóng tăng tốc (chúi nhẹ), vệt bánh từ bánh sau
                cv.pitches.Add(new Pitch { at = Time, amp = _cfg.leaveBrakePitch, dur = _cfg.leaveBrakePitchDuration });
                cv.pitches.Add(new Pitch { at = Time + _cfg.leaveLaunchPause, amp = -_cfg.leaveLaunchPitch, dur = _cfg.leaveLaunchPitchDuration });
                var exit = new Vector3(-_cfg.leaveEdgeX - 4f, 0f, laneEnd.z);
                StartMove(cv, new[] { laneEnd, exit }, Vector3.Distance(laneEnd, exit) / _cfg.leaveExitSpeed, Quaternion.identity, false, _cfg.leaveLaunchPause, gone); cv.move.ease = 2;
                SpawnSkid(laneEnd, Vector3.left, Time + _cfg.leaveLaunchPause);
            };
            // B10 (góp ý 11, 1f thấy f146): làn trên chỉ 1 xe một lúc: xe lùi ra chờ tới khi làn trống (xe Claw đang đứng / rời, xe khác đang lùi / phóng)
            LaneDepart(cv, () => { StartMove(cv, pts, len / Mathf.Max(0.5f, _cfg.leaveReverseSpeed), Quaternion.identity, false, 0.1f, driveOff); cv.move.reverse = true; cv.move.ease = 1; });
        }

        // ---------------------------------------------------------------- B10: hàng đợi làn trên
        readonly List<KeyValuePair<CarV, Action>> _laneQueue = new List<KeyValuePair<CarV, Action>>();
        /// <summary>Xe đang ở dải làn trên (|z - leaveLaneZ| < laneClearZ) và còn trong khung (|x| < leaveEdgeX).</summary>
        bool InLane(CarV c) { return c.tr != null && Mathf.Abs(c.tr.localPosition.z - _cfg.leaveLaneZ) < _cfg.laneClearZ && Mathf.Abs(c.tr.localPosition.x) < _cfg.leaveEdgeX; }
        /// <summary>Làn trên đang có xe khác (ngoài self): xe Claw đang đổ ở làn (_laneCars), xe đang chiếm làn (onLane, chưa ra khỏi khung), hoặc xe rời đang đứng trên làn.</summary>
        bool LaneBusyExcept(CarV self)
        {
            if (_laneCars.Count > 0) return true;
            foreach (var c in _cars) if (c != self && c.tr != null && ((c.onLane && Mathf.Abs(c.tr.localPosition.x) < _cfg.leaveEdgeX) || (c.leaving && InLane(c)))) return true;
            return false;
        }
        /// <summary>Xin rời qua làn trên: chạy ngay nếu làn trống, không thì xếp hàng (xe đứng yên tại chỗ, xe kế vào bến cũng chờ theo). start phải gọi StartMove cho xe; xe giữ làn tới khi ra khỏi khung / bị Kill.</summary>
        void LaneDepart(CarV cv, Action start)
        {
            cv.laneWait = true; _laneQueue.Add(new KeyValuePair<CarV, Action>(cv, start)); TickLaneQueue();
        }
        void TickLaneQueue()
        {
            while (_laneQueue.Count > 0)
            {
                // ưu tiên xe đang đứng sẵn trên làn (xe Claw chờ rời) để xe bến không lùi vào nó; còn lại theo thứ tự xin
                int pick = 0; for (int i = 0; i < _laneQueue.Count; i++) if (InLane(_laneQueue[i].Key)) { pick = i; break; }
                var q = _laneQueue[pick];
                if (q.Key.tr == null) { _laneQueue.RemoveAt(pick); continue; }
                if (LaneBusyExcept(q.Key)) break;
                _laneQueue.RemoveAt(pick);
                q.Key.laneWait = false; q.Key.onLane = true; q.Value();
                if (q.Key.move != null && !q.Key.leaving) q.Key.onLane = false;
                FeelLog("B10 xe #" + q.Key.car.Id + " được vào làn trên, còn chờ " + _laneQueue.Count);
            }
        }

        void OnCarEntered(Car car, int slot)
        {
            // chỉ xảy ra sau khi dựng: xe đầu cột xuống ô trống, các xe sau dồn lên (C3)
            if (_queue[slot].Count == 0) return;
            var cv = _queue[slot][0]; _queue[slot].RemoveAt(0);
            if (cv.car != car) { Log?.Invoke("CẢNH BÁO: xe vào ô không khớp hàng chờ"); return; }
            cv.slotPos = SlotPos(slot); _slotCar[slot] = cv;
            cv.tr.localScale = Vector3.one * _cfg.busScale; cv.baseScale = _cfg.busScale;
            FeelCarEnter(cv);   // M7
            // B10: xe cũ của ô này còn chờ làn trên (chưa lùi ra) → xe mới và cả cột chưa đếm delay xuống
            CarV old = null; foreach (var c in _cars) if (c.leaving && c.laneWait && (c.slotPos - cv.slotPos).sqrMagnitude < 0.01f) { old = c; break; }
            // C15 (góp ý 04): xe kế chỉ bắt đầu xuống khi xe cũ sắp khuất (clip: hồng xuống 1.5 s sau khi xanh rời)
            StartTravel(cv, SlotPos(slot), SlotRot, _cfg.nextCarDelay, true);   // góp ý 07: tăng tốc, phanh cuối, nghiêng khi cua (thay queueToSlotDuration đều)
            cv.move.waitFor = old;
            for (int k = 0; k < _queue[slot].Count; k++)
            {
                StartMove(_queue[slot][k], new[] { _queue[slot][k].tr.localPosition, QueuePos(slot, k) }, _cfg.queueToSlotDuration, Quaternion.identity, true, _cfg.nextCarDelay + 0.1f);
                _queue[slot][k].move.waitFor = old;
            }
        }

        void OnBarrier(int slot)
        {
            if (_a.barrierMesh == null) return;
            var go = new GameObject("Barrier" + slot); go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.barrierMesh;
            // góp ý 14 (BuzzyProbe.Meshes, probe14): Barrier.obj là rào đường 3D thật (thanh ngang + 4 chân); UV của nó KHÔNG trỏ vào bảng màu Color.png (lấy ra tím nhạt) → tô sọc chéo đỏ / trắng theo UV (mẫu: đỏ ~60 %, 2 sọc trắng)
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat };
            int per = Mathf.Max(2, _cfg.barrierStripePeriod);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, ((x - y + 256) / per) % 3 == 0 ? _cfg.barrierWhite : _cfg.barrierRed);
            tex.Apply();
            var m = Mat("Custom/BuzzyEnvironment", Color.white); m.mainTexture = tex; go.AddComponent<MeshRenderer>().sharedMaterial = m;
            var s = SlotPos(slot);
            // góp ý 14: rào lệch về mép dưới ô (gần người chơi, theo hướng mũi), thanh song song cạnh ngắn ô, xoay theo góc ô; barrierTilt giữ 0 (mesh đã đứng)
            go.transform.localPosition = s + SlotRot * Vector3.back * _cfg.barrierForward; go.transform.localRotation = Quaternion.Euler(0f, _cfg.slotYaw + _cfg.barrierYawOffset, 0f) * Quaternion.Euler(_cfg.barrierTilt, 0f, 0f);
            // góp ý 16: rào nhỏ → to dần, rơi xuống ô, nhún có quán tính (số chờ 3c đo clip Lv.1)
            _pops.Add(new Pop { tr = go.transform, at = Time + _cfg.barrierDelay, dur = Mathf.Max(0.05f, _cfg.barrierAppearDuration), scale = Vector3.one * _cfg.barrierScale,
                drop = _cfg.barrierDropHeight, dropDur = _cfg.barrierDropDuration, overshoot = _cfg.barrierOvershoot, bounce = _cfg.barrierBounceHeight, startScale = _cfg.barrierStartScale, basePos = go.transform.localPosition });
            FeelLog("rào ô " + slot + " tại " + go.transform.localPosition.ToString("F2"));
            go.transform.localScale = Vector3.zero;
        }

        // ---------------------------------------------------------------- thao tác
        /// <summary>Chạm cụm màu: pixel bay lên xe cùng màu; không có xe khớp thì rung (C11).</summary>
        public bool TapColor(int color, int tx = -1, int ty = -1)
        {
            if (Won || Time < _inputOpenAt) return false;
            if (color >= 100 && tx < 0) Solver.Decode(color, out color, out tx, out ty);   // mã bước của Solver.FindOrder (màu + ô chạm)
            var r = tx >= 0 ? Game.TapAt(color, tx, ty) : Game.Tap(color);   // tx, ty: ô được chạm (luật 1c dùng làm gốc thứ tự)
            if (!r.Matched) { if (Vibrate != null) Vibrate(); FeelWrong(color); Log?.Invoke("t=" + Time.ToString("0.00") + " Tap màu " + color + " -> NoMatch (rung)"); return false; }
            TapCount++;
            Log?.Invoke("t=" + Time.ToString("0.00") + " Tap màu " + color + " -> " + r.Pixels.Count + " pixel lên xe #" + r.Car.Id + " (ô " + r.Slot + ")");
            StartWalks(r);   // M7 góp ý 04: đi bộ trên mặt đất (StartFlights chỉ còn cho Sky Grab)
            FeelTap(r);   // M7: tiếng cụm, xe đầy lắc
            return true;
        }

        void StartFlights(TapResult r, float delay = 0f)
        {
            var cv = ViewOf(r.Car);
            int n = r.Pixels.Count;
            float spread = Mathf.Min(_cfg.flightSpread, _cfg.flightSpread * n / 50f + 0.4f);
            for (int i = 0; i < n; i++)
            {
                Pass p; if (!_pass.TryGetValue(r.Pixels[i].X * 1000 + r.Pixels[i].Y, out p)) continue;
                p.flying = true;
                if (p.hidden) { p.hidden = false; p.mr.sharedMaterial = PassMat(r.Pixels[i].Color); }   // Mystery không chặn: lộ màu lúc bay
                float jit = 1f + _cfg.walkSpeedJitter * ((float)_rnd.NextDouble() * 2f - 1f);
                _flights.Add(new Flight { p = p, car = cv, start = Time + delay + (n <= 1 ? 0f : (i / (float)(n - 1)) * spread), dur = _cfg.flightDuration * jit, from = p.home, phase = (float)_rnd.NextDouble() * 6.28f, side = _rnd.Next(2) == 0 ? -1f : 1f, tapAt = Time + delay });
            }
        }

        // ---------------------------------------------------------------- vòng lặp
        void Update()
        {
            HandleInput();
            Tick(UnityEngine.Time.deltaTime * TimeScale);
        }

        public void Tick(float dt)
        {
            Time += dt;
            // pixel xuất hiện
            TickIntro();
            foreach (var kv in _pass)
            {
                var p = kv.Value; if (p.flying) continue;
                float u = Mathf.Clamp01((Time - p.appearAt) / _cfg.appearSpreadDuration.y);
                SetPassScale(p, EaseOutBack(u));
                var basePos = p.home;
                if (p.loose && u >= 1f) basePos += new Vector3(0f, _cfg.looseBob * Mathf.Max(0f, Mathf.Sin(Time * _cfg.looseBobSpeed + p.phase)), 0f);   // nhảy lên rồi nghỉ
                // J6 (M7): chạm sai → cụm màu đó rung ngang; đặt vị trí từ gốc (không cộng dồn mỗi khung), khung rung cuối trả về gốc
                bool shaking = _shakeColor == p.d.Color;
                if ((p.loose && u >= 1f) || shaking) p.tr.localPosition = basePos + Vector3.right * (shaking ? WrongShakeOffset(p.d.Color) : 0f);
            }
            if (_shakeColor >= 0 && Time - _shakeAt >= _cfg.wrongShakeDuration) _shakeColor = -1;   // J6: hết rung (sau khi mọi pixel đã về gốc ở khung này)
            for (int i = 0; i < 3; i++)
            {
                if (_pMark[i] == null) continue;
                // V17: chữ P chỉ khi không còn xe nào trên ô (xe kế chưa đỗ xong, xe rời đã đi khỏi ô)
                bool show = (_slotCar[i] == null || _slotCar[i].move != null) && !Game.Barrier[i] && !Won;
                if (show) foreach (var c in _cars) if (Vector3.Distance(c.tr.localPosition, SlotPos(i)) < _cfg.pMarkSize.y * 0.6f) { show = false; break; }
                if (_pMark[i].activeSelf != show) _pMark[i].SetActive(show);
            }
            // pixel bay
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                float u = (Time - f.start) / f.dur;
                if (u < 0f) { f.p.tr.localPosition = f.p.home + Vector3.up * PickLift(f.tapAt); continue; }   // J1 (M7): cụm đã chọn nhấc lên chờ lượt
                var to = f.car.tr.position + new Vector3(0f, 1.4f, 0f);
                float e = Mathf.Clamp01(u); e = e * e * (3f - 2f * e);
                var flat = to - f.from; flat.y = 0f; var perp = Vector3.Cross(Vector3.up, flat.normalized);
                float wob = _cfg.walkWobble * Mathf.Sin(e * Mathf.PI * 2f + f.phase) * (1f - e) * f.side;
                float hop = _cfg.walkHop * Mathf.Abs(Mathf.Sin(e * Mathf.PI * _cfg.walkHops)) * (1f - e * 0.6f);
                var pos = Vector3.Lerp(f.from, to, e) + perp * wob + Vector3.up * (_cfg.flightArc * 4f * e * (1f - e) + hop + _cfg.pickLift * (1f - e));   // J1: hạ dần từ độ nhấc
                f.p.tr.localPosition = pos;
                f.p.tr.localScale = BlockScale * Mathf.Lerp(1f, 0.35f, e * e);
                if (flat.sqrMagnitude > 0.001f) f.p.tr.localRotation = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(0f, 180f + 25f * Mathf.Sin(e * 12f + f.phase), 0f);
                if (u >= 1f)
                {
                    _flights.RemoveAt(i);
                    Kill(f.p.tr.gameObject); ForgetPass(f.p);
                    Game.Arrive(f.car.car, f.p.d);
                }
            }
            // xe
            for (int i = _cars.Count - 1; i >= 0; i--) if (i < _cars.Count) StepCar(_cars[i], dt);
            TickLaneQueue();   // B10
            // pop (rào)
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i]; float u = Mathf.Clamp01((Time - p.at) / p.dur);
                if (p.tr != null && p.drop <= 0f) p.tr.localScale = p.scale * EaseOutBack(u);
                else if (p.tr != null)
                {
                    // góp ý 16 / J7 (3c đo clip Lv.1 26,63 + 29,67): t0 cỡ 0,2 cao hơn chỗ đứng 0,7 cao rào → rơi + to dần, chạm đất ở dropDur (0,10 s) cỡ 1,0
                    // → nở quá 1,15 (+0,13–0,20 s) + nẩy 1 lần 25 % cao rào, về đất +0,30 s → cỡ 1,0 ở dur (0,45 s)
                    float t = Time - p.at; float s, y;
                    if (t < p.dropDur) { float f = Mathf.Clamp01(t / Mathf.Max(0.01f, p.dropDur)); s = Mathf.Lerp(p.startScale, 1f, f); y = p.drop * (1f - f * f); }
                    else
                    {
                        float tt = Mathf.Clamp01((t - p.dropDur) / Mathf.Max(0.01f, p.dur - p.dropDur));
                        s = p.overshoot * Mathf.Sin(tt * Mathf.PI) * (1f - tt);                           // nở NGANG quá (clip 1,10–1,15), cao gần như không ép (≤ 5 %), đỉnh ≈ +0,2 s rồi về 1
                        float bt = Mathf.Clamp01(tt / 0.57f); y = p.bounce * Mathf.Sin(bt * Mathf.PI);        // nẩy 1 lần, về đất ở ~57 % pha sau (+0,30 s)
                    }
                    p.tr.localPosition = p.basePos + Vector3.up * y;
                    if (t < p.dropDur) p.tr.localScale = p.scale * s;
                    else p.tr.localScale = new Vector3(p.scale.x * (1f + s), p.scale.y * (1f - s * 0.3f), p.scale.z * (1f + s));   // ngang +ov, cao −0,3·ov (≤ 5 %)
                }
                if (u >= 1f) _pops.RemoveAt(i);
            }
            // bong bóng Full theo xe ở ô
            for (int i = 0; i < 3; i++)
            {
                var cv = _slotCar[i];
                bool show = cv != null && cv.car.IsFull && !cv.leaving && cv.move == null;   // góp ý 08: xe về bến đỗ xong mới hiện Full (C11)
                _fullBubbles[i].gameObject.SetActive(show);
                if (_fullSigns[i] != null) _fullSigns[i].gameObject.SetActive(show);
                if (show && _fullSigns[i] != null)
                {
                    // góp ý 14: đuôi biển chạm đất ở góc trước-trái xe (theo khung: trái = Cross(up, mũi)), mặt biển quay về camera; chữ UI đặt tại tâm đĩa
                    var left = Vector3.Cross(Vector3.up, SlotNose);
                    var sg = _fullSigns[i]; sg.localPosition = cv.slotPos + SlotNose * _cfg.fullSignOffset.y + left * _cfg.fullSignOffset.x;
                    sg.rotation = Quaternion.LookRotation(_cam.transform.forward, Vector3.up);
                    sg.position -= sg.up * _cfg.fullSignSink;   // lún đuôi: chân dài thành đuôi giọt ngắn, đĩa hạ ngang cản trước
                    var vp = _cam.WorldToViewportPoint(sg.TransformPoint(0f, _cfg.fullSignDiscY, 0f));
                    _fullBubbles[i].anchoredPosition = new Vector2(vp.x * _canvasSize.x, -(1f - vp.y) * _canvasSize.y);
                }
                else if (show)
                {
                    var vp = _cam.WorldToViewportPoint(cv.tr.position);
                    _fullBubbles[i].anchoredPosition = new Vector2((vp.x - 0.11f) * _canvasSize.x, -(1f - vp.y + 0.005f) * _canvasSize.y);
                }
            }
            TickWalks(dt);   // M7 góp ý 04
            TickFeel(dt);   // M7
            TickMechanics(dt);
            // thắng
            if (!Won && Game.Status == GameStatus.Won && _flights.Count == 0 && _walks.Count == 0 && _vanish.Count == 0 && !BoosterBusy)
            {
                bool anyLeaving = false; foreach (var c in _cars) if (c.leaving) anyLeaving = true;
                if (!anyLeaving) { Won = true; WonAt = Time; ShowWin(); }
            }
            CheckLose();
            TickPopups(dt);
        }

        void StepCar(CarV cv, float dt)
        {
            var mv = cv.move; if (mv == null) return;
            if (mv.waitFor != null) { if (mv.waitFor.laneWait) return; mv.waitFor = null; }   // B10: xe cũ chưa rời khỏi ô thì chưa đếm delay
            if (mv.delay > 0f) { mv.delay -= dt; return; }
            mv.t += dt;
            float u = Mathf.Clamp01(mv.t / mv.dur);
            float e;
            if (mv.prof != null) { float fi = u * 32f; int k = Mathf.Clamp((int)fi, 0, 31); e = Mathf.Lerp(mv.prof[k], mv.prof[k + 1], fi - k); }
            else if (mv.ease == 1) e = 1f - Mathf.Pow(1f - u, 3f);                 // phanh: vọt nhanh rồi hãm gấp (góp ý 06)
            else if (mv.ease == 2) e = u * u;                                   // phóng: tăng tốc đều (gốc _driveOffAcceleration 40)
            else if (cv.leaving) e = u * u * (3f - 2f * u) * 0.35f + u * 0.65f;
            else e = 1f - (1f - u) * (1f - u);                                 // vào ô: giảm tốc
            float total = mv.cum[mv.cum.Length - 1];
            float d = total * e;
            int seg = 1; while (seg < mv.pts.Length - 1 && mv.cum[seg] < d) seg++;
            float segLen = Mathf.Max(0.0001f, mv.cum[seg] - mv.cum[seg - 1]);
            float lu = Mathf.Clamp01((d - mv.cum[seg - 1]) / segLen);
            var dir = (mv.pts[seg] - mv.pts[seg - 1]).normalized;
            cv.tr.localPosition = Vector3.Lerp(mv.pts[seg - 1], mv.pts[seg], lu);
            // góp ý 07: nghiêng khi cua theo tốc độ đổi hướng (gốc _travelBankStrength 0.5, max 18°), gỡ góc chúi / ngửa của khung trước khỏi gốc quay
            var cur = cv.tr.localRotation * Quaternion.Inverse(cv.lastPitch); cv.lastPitch = Quaternion.identity;
            float rollTarget = 0f;
            if (mv.bank && dir.sqrMagnitude > 0.0001f && cv.prevDir.sqrMagnitude > 0.0001f && dt > 0f)
            {
                float yawRate = Vector3.SignedAngle(cv.prevDir, dir, Vector3.up) / dt;   // độ / s, dương = rẽ phải
                rollTarget = Mathf.Clamp(-yawRate * _cfg.travelBankStrength, -_cfg.travelMaxBank, _cfg.travelMaxBank);
            }
            if (dir.sqrMagnitude > 0.0001f) cv.prevDir = dir;
            cv.roll = Mathf.Lerp(cv.roll, rollTarget, 1f - Mathf.Exp(-_cfg.travelBankSmoothing * dt));
            Quaternion want = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(mv.reverse ? dir : -dir) * Quaternion.Euler(0f, 0f, cv.roll) : cur;   // reverse: lùi, mũi ngược hướng đi
            if (mv.useEndRot && u > 0.7f) want = Quaternion.Slerp(want, mv.endRot, Mathf.InverseLerp(0.7f, 1f, u));
            if (!mv.keepRot) cv.tr.localRotation = mv.ease == 1 ? want : Quaternion.Slerp(cur, want, 1f - Mathf.Exp(-10f * dt));   // lùi vòng: bám tiếp tuyến ngay (cung ngắn, nhanh)
            else cv.tr.localRotation = cur;
            if (u >= 1f)
            {
                cv.tr.localPosition = mv.pts[mv.pts.Length - 1];
                if (mv.useEndRot) { cv.tr.localRotation = mv.endRot; cv.roll = 0f; }
                cv.move = null;
                if (mv.useEndRot && !cv.leaving) FeelParked(cv);   // J3 (M7): đỗ xong nhún
                if (mv.done != null) mv.done();
            }
        }

        // màn thắng + popup: BuzzyGameView.Popups.cs (M5)

        static float EaseOutBack(float x)
        {
            if (x <= 0f) return 0f; if (x >= 1f) return 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
