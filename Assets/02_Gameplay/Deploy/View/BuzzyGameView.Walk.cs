using System;
using System.Collections.Generic;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>
    /// M7 (góp ý 04): hành khách ĐI BỘ trên mặt đất tới xe như clip gốc (config gốc PassengerEntity: _moveToCarSpeed, _crowdFollowGap,
    /// _crowdLaneBias, _jumpHeight / _jumpDuration, _stragglerMigrationDelay), thay cho bay vòng cung (giữ StartFlights cho Sky Grab).
    /// Luật 1b (design-decisions 19): chạm chọn hết màu (Core giữ chỗ), nhưng mỗi pixel chỉ xuất phát khi có đường đi qua ô rỗng ra ngoài;
    /// lớp ngoài đi trước, lớp trong chờ tại chỗ; pixel bị bao kín bởi màu khác (kẹt hẳn) chờ stragglerDelay rồi nhảy qua ra ô rỗng gần nhất.
    /// Đường đi: Dijkstra 8 hướng trên lưới mở rộng (bảng + lề + tới trước cửa xe), rồi kéo thẳng (string pulling). Số ở BuzzyLayoutConfig mục "Đi bộ".
    /// </summary>
    public partial class BuzzyGameView
    {
        class Walk
        {
            public Pass p; public CarV car; public int phase;          // 0 chờ tại chỗ, 1 nhảy qua (straggler), 2 đi bộ, 3 nhảy lên xe
            public float start, speed, lane, phaseStart, hopDur; public Vector3 hopFrom, hopTo, jumpFrom;
            public int order, groupN; public bool crossed;   // góp ý 13: thứ tự / sĩ số trong lượt (xếp chỗ chờ trước mép lề); đã băng qua đường tới hông xe
            public List<Vector3> pts; public float[] cum; public float total; public float dist;
            public Vector3 pos, dir, off;   // vị trí danh nghĩa trên đường, hướng đi, độ lệch do đẩy nhau (góp ý 05)
            public bool moving; public int slot;   // slot: thứ tự xuất phát → chỗ đứng trong đội hình trước cửa (góp ý 06)
        }
        readonly List<Walk> _walks = new List<Walk>();
        readonly HashSet<int> _pendingCells = new HashSet<int>();   // ô của pixel đã chọn nhưng chưa xuất phát (vẫn là vật cản)
        int _walkSeq, _groupLeft;

        // ---------------------------------------------------------------- lưới mở rộng
        int M { get { return Mathf.Max(1, _cfg.walkGridMargin); } }
        bool CellOccupied(int gx, int gy)
        {
            var b = Game.Board;
            if (gx < 0 || gy < 0 || gx >= b.W || gy >= b.H) return false;
            return b.ColorAt(gx, gy) != Board.Empty || b.IsWall(gx, gy) || _pendingCells.Contains(gx * 1000 + gy);
        }
        Vector3 CellWorld(float gx, float gy) { return new Vector3(_cellX0 + gx * CellX, 0f, _boardZ0 + gy * CellZ); }
        Vector2Int WorldCell(Vector3 w) { return new Vector2Int(Mathf.RoundToInt((w.x - _cellX0) / CellX), Mathf.RoundToInt((w.z - _boardZ0) / CellZ)); }

        /// <summary>Trường khoảng cách từ ô đích qua ô trống (8 hướng, không cắt góc). Chỉ số [gx+M, gy+M], -1 = không tới được.</summary>
        float[,] DistField(Vector2Int goal, int gyMax, out int ox, out int oy)
        {
            var b = Game.Board; int m = M; ox = m; oy = m;
            int nx = b.W + 2 * m, ny = gyMax + 1 + 2 * m;
            var d = new float[nx, ny]; for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) d[i, j] = -1f;
            Func<int, int, bool> open = (gx, gy) => gx >= -m && gy >= -m && gx < b.W + m && gy <= gyMax + m && !CellOccupied(gx, gy);
            // W3 (so clip, walk-compare-fb08): đi cách rìa khối 1–2 ô: ô kề khối (8 hướng) tốn thêm walkEdgePenalty, ô cách 2 tốn một nửa
            Func<int, int, float> near = (gx, gy) =>
            {
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if ((dx != 0 || dy != 0) && CellOccupied(gx + dx, gy + dy)) return _cfg.walkEdgePenalty;
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) if ((Mathf.Abs(dx) == 2 || Mathf.Abs(dy) == 2) && CellOccupied(gx + dx, gy + dy)) return _cfg.walkEdgePenalty * 0.5f;
                return 0f;
            };
            if (!open(goal.x, goal.y)) return d;
            var q = new SortedSet<(float, int, int, int)>(); int seq = 0;
            d[goal.x + m, goal.y + m] = 0f; q.Add((0f, seq++, goal.x, goal.y));
            while (q.Count > 0)
            {
                var cur = q.Min; q.Remove(cur); int cx = cur.Item3, cy = cur.Item4; float cd = cur.Item1;
                if (cd > d[cx + m, cy + m]) continue;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int tx = cx + dx, ty = cy + dy; if (!open(tx, ty)) continue;
                    if (dx != 0 && dy != 0 && (!open(cx + dx, cy) || !open(cx, cy + dy))) continue;   // không cắt góc
                    float nd = cd + (dx != 0 && dy != 0 ? 1.4142f : 1f) + near(tx, ty);
                    if (d[tx + m, ty + m] < 0f || nd < d[tx + m, ty + m]) { d[tx + m, ty + m] = nd; q.Add((nd, seq++, tx, ty)); }
                }
            }
            return d;
        }

        /// <summary>Đường từ ô xuất phát (ô của pixel, coi là mở) xuống dốc trường khoảng cách tới đích, rồi kéo thẳng.</summary>
        List<Vector3> BuildPath(Vector2Int from, float[,] d, int ox, int oy, Vector3 goalWorld)
        {
            var cells = new List<Vector2Int> { from };
            int nx = d.GetLength(0), ny = d.GetLength(1);
            Func<int, int, float> D = (gx, gy) => { int i = gx + ox, j = gy + oy; return i < 0 || j < 0 || i >= nx || j >= ny ? -1f : d[i, j]; };
            var cur = from; int guard = 4000; float best = float.MaxValue;
            // ô đầu: láng giềng 4 hướng tới được có khoảng cách nhỏ nhất
            while (guard-- > 0)
            {
                int bx = cur.x, by = cur.y; best = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (cells.Count == 1 && dx != 0 && dy != 0) continue;   // bước đầu ra khỏi ô mình: 4 hướng
                    float v = D(cur.x + dx, cur.y + dy); if (v < 0f) continue;
                    if (v < best) { best = v; bx = cur.x + dx; by = cur.y + dy; }
                }
                if (best == float.MaxValue) return null;
                cur = new Vector2Int(bx, by); cells.Add(cur);
                if (best <= 0f) break;
            }
            // kéo thẳng: bỏ điểm giữa khi đoạn thẳng đi qua toàn ô mở (lấy mẫu 1/4 ô)
            var pts = new List<Vector3>();
            int a = 0; pts.Add(CellWorld(cells[0].x, cells[0].y));
            while (a < cells.Count - 1)
            {
                int c = cells.Count - 1;
                for (; c > a + 1; c--) if (LineClear(cells[a], cells[c], cells[0])) break;
                pts.Add(CellWorld(cells[c].x, cells[c].y)); a = c;
            }
            pts[pts.Count - 1] = goalWorld;
            return pts;
        }

        bool LineClear(Vector2Int a, Vector2Int b, Vector2Int self)
        {
            // ngoài bảng (cả 2 đầu cách bảng ≥ 1 ô): đoạn thẳng phải cách khối ≥ 1 ô (W3); trong bảng: chỉ cần không xuyên ô có khối
            var bd = Game.Board; Func<Vector2Int, bool> outside = c => c.x < -1 || c.y < -1 || c.x > bd.W || c.y > bd.H || !InBoardBand(c);
            int clear = outside(a) && outside(b) ? 1 : 0;
            float len = Vector2Int.Distance(a, b); int n = Mathf.CeilToInt(len * 4f);
            for (int i = 1; i < n; i++)
            {
                float u = i / (float)n; int gx = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, u)), gy = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, u));
                for (int dx = -clear; dx <= clear; dx++) for (int dy = -clear; dy <= clear; dy++)
                    if ((gx + dx != self.x || gy + dy != self.y) && CellOccupied(gx + dx, gy + dy)) return false;
            }
            return true;
        }

        /// <summary>Ô nằm trong hình bao bảng (kể cả lề 1 ô).</summary>
        bool InBoardBand(Vector2Int c) { var bd = Game.Board; return c.x >= -1 && c.y >= -1 && c.x <= bd.W && c.y <= bd.H && (CellOccupied(c.x, c.y) || NearBlock(c)); }
        bool NearBlock(Vector2Int c) { for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (CellOccupied(c.x + dx, c.y + dy)) return true; return false; }

        bool ReachableNow(int gx, int gy, bool[,] openField, int ox, int oy)
        {
            var b = Game.Board;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = gx + dx[i], ny = gy + dy[i];
                if (nx < 0 || ny < 0 || nx >= b.W || ny >= b.H) return true;
                if (openField[nx + ox, ny + oy]) return true;
            }
            return false;
        }

        /// <summary>Ô rỗng (trong bảng) thông ra ngoài qua ô rỗng, với vật cản hiện tại (kể cả pixel chờ).</summary>
        bool[,] OpenField(out int ox, out int oy)
        {
            var b = Game.Board; int m = 1; ox = m; oy = m;
            var o = new bool[b.W + 2, b.H + 2]; var q = new Queue<Vector2Int>();
            for (int x = -1; x <= b.W; x++) for (int y = -1; y <= b.H; y++)
                if ((x < 0 || y < 0 || x >= b.W || y >= b.H)) { o[x + 1, y + 1] = true; q.Enqueue(new Vector2Int(x, y)); }
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = c.x + dx[i], ny = c.y + dy[i];
                    if (nx < 0 || ny < 0 || nx >= b.W || ny >= b.H || o[nx + 1, ny + 1] || CellOccupied(nx, ny)) continue;
                    o[nx + 1, ny + 1] = true; q.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return o;
        }

        // ---------------------------------------------------------------- lên lịch xuất phát
        /// <summary>Hông xe lộ ra (clip Lv.1: đám đông dồn vào góc trước-phải xe theo hướng mũi, phía camera-trái): phải = Cross(up, mũi).</summary>
        Vector3 SlotNose { get { return SlotRot * Vector3.back; } }
        Vector3 SlotSide { get { return Vector3.Cross(Vector3.up, SlotNose) * Mathf.Sign(_cfg.boardSide == 0 ? 1f : _cfg.boardSide); } }
        /// <summary>Điểm tập kết cạnh Ô BẾN (không theo xe đang chạy): phía hông xe lộ ra, trước mũi một chút; người tới trước đứng chờ xe đỗ (góp ý 05, 08).</summary>
        Vector3 BoardPoint(CarV cv) { return cv.slotPos + SlotSide * (_cfg.boardSideOffset + _cfg.boardPointOffset * 0.5f) + SlotNose * _cfg.boardPointOffset; }
        /// <summary>Có xe đang chuyển động trong vùng bến (vào ô, lùi ra, chạy trên làn; dưới cột chờ, trong khung): đám đông chờ ở lề không băng qua (góp ý 13).</summary>
        bool BayBusy()
        {
            foreach (var c in _cars)
                if (c.tr != null && c.move != null && c.move.delay <= 0f && c.tr.localPosition.z < _cfg.leaveLaneZ + _cfg.laneClearZ && Mathf.Abs(c.tr.localPosition.x) < _cfg.leaveEdgeX) return true;
            return false;
        }
        /// <summary>Góp ý 13 (clip 6/7 ca): chỗ đứng sát hông trái khung, góc đuôi, trước khi nhảy (đích của đoạn băng qua đường sau khi xe đỗ).</summary>
        Vector3 FlankPoint(CarV cv) { return cv.slotPos + SlotSide * (_cfg.boardSideOffset + _cfg.walkMinDist) + SlotNose * _cfg.boardFrontOffset; }
        /// <summary>Góp ý 13: chỗ chờ khi xe chưa đỗ: phía bảng của mép lề (waitLineZ), hàng đầu cách mép waitLineOffset ô, dàn ngang tâm = ô bến đích, tối đa waitSpread ô rộng, cột xếp từ giữa ra, số cột = sĩ số / waitRows.</summary>
        Vector3 WaitGoal(Walk w)
        {
            float step = _cfg.walkMinDist * 1.05f;
            int maxCols = Mathf.Max(1, Mathf.FloorToInt(_cfg.waitSpread * CellX / step));
            int cols = Mathf.Clamp(Mathf.CeilToInt(w.groupN / (float)Mathf.Max(1, _cfg.waitRows)), 1, maxCols);
            int row = w.order / cols, c = w.order % cols; int col = (c % 2 == 0 ? 1 : -1) * ((c + 1) / 2);   // 0, +1, -1, +2, -2...
            float z = _cfg.waitLineZ - _cfg.waitLineOffset * CellZ - row * step;
            return new Vector3(w.car.slotPos.x + col * step, 0f, z);
        }
        /// <summary>Điểm nhảy vào xe: hông xe lộ ra, thấp (cửa), hơi về phía mũi.</summary>
        Vector3 BoardTarget(CarV cv) { return cv.tr.position + SlotSide * _cfg.boardSideOffset + SlotNose * _cfg.boardFrontOffset + Vector3.up * _cfg.boardHeight; }

        /// <summary>Chạm: lên lịch đi bộ cho cả cụm Core trả về (thứ tự lớp ngoài / gần xe). Mỗi lượt: pixel nào có đường ra thì xuất phát cách nhau walkGap;
        /// lặp tới khi không ai ra thêm được; còn lại là straggler (nhảy qua sau stragglerDelay).</summary>
        void StartWalks(TapResult r, float delay = 0f)
        {
            var cv = ViewOf(r.Car); if (cv == null) { StartFlights(r, delay); return; }
            var pend = new List<Walk>();
            foreach (var px in r.Pixels)
            {
                Pass p; if (!_pass.TryGetValue(px.X * 1000 + px.Y, out p)) continue;
                p.flying = true;
                if (p.hidden) { p.hidden = false; p.mr.sharedMaterial = PassMat(px.Color); }
                _pendingCells.Add(px.X * 1000 + px.Y);
                float jit = 1f + _cfg.walkSpeedVariance * ((float)_rnd.NextDouble() * 2f - 1f);
                pend.Add(new Walk { p = p, car = cv, phase = 0, speed = _cfg.walkSpeed * jit });
            }
            float t = Time + delay; int released = 0, passes = 0; _groupLeft = 0;
            while (pend.Count > 0 && passes++ < 200)
            {
                int ox, oy; var open = OpenField(out ox, out oy); bool any = false;
                // W4 (clip): trong lượt, người gần xe đi trước (sắp theo khoảng cách tới ô bến)
                var sp = cv.slotPos; pend.Sort((a, b) => (a.p.home - sp).sqrMagnitude.CompareTo((b.p.home - sp).sqrMagnitude));
                for (int i = 0; i < pend.Count; i++)
                {
                    var w = pend[i]; if (!ReachableNow(w.p.d.X, w.p.d.Y, open, ox, oy)) continue;
                    // W2: xếp làn luân phiên (đi 3–4 hàng ngang như clip), mỗi người cách walkGap
                    int nl = Mathf.Max(1, _cfg.walkLanes); w.lane = ((released % nl) - (nl - 1) * 0.5f) * _cfg.walkMinDist * _cfg.walkLaneSpacing + ((float)_rnd.NextDouble() - 0.5f) * 0.1f;
                    w.slot = _walkSeq++;
                    // W2 (clip): đi thành túm 2–4 người sát nhau, giữa các túm cách walkGap
                    if (_groupLeft <= 0) { _groupLeft = _rnd.Next(Mathf.Max(1, _cfg.walkGroupMin), Mathf.Max(_cfg.walkGroupMin, _cfg.walkGroupMax) + 1); t += _cfg.walkGap * (0.7f + 0.6f * (float)_rnd.NextDouble()); }
                    w.start = t; t += _cfg.walkGroupGap; _groupLeft--; w.phase = 0;
                    _pendingCells.Remove(w.p.d.X * 1000 + w.p.d.Y);   // từ lúc này ô coi như trống cho người sau
                    w.order = released; _walks.Add(w); pend.RemoveAt(i); i--; any = true; released++;
                }
                if (!any) break;
            }
            foreach (var w in _walks) if (w.car == cv && w.start >= Time + delay - 0.001f && w.phase == 0) w.groupN = released;   // sĩ số lượt này (xếp chỗ chờ)
            // kẹt hẳn: chờ rồi nhảy qua
            float ts = t + _cfg.stragglerDelay; int k = 0;
            foreach (var w in pend) { w.start = ts + (k++) * _cfg.walkGap * 2f; w.phase = -1; _walks.Add(w); }
            FeelLog("đi bộ: " + released + " xuất phát theo đường trống, " + pend.Count + " kẹt phải nhảy qua, tổng " + (t - Time - delay).ToString("0.00") + " s xả");
        }

        /// <summary>Tính đường đi từ vị trí hiện tại tới trước cửa xe (gọi lúc xuất phát để lấy vật cản mới nhất).</summary>
        bool PlanWalk(Walk w, Vector3 fromWorld)
        {
            // góp ý 06: đội hình trước cửa: hàng (row) lùi về phía bảng để người tới trước / sau không đứng chồng
            int nl = Mathf.Max(1, _cfg.walkLanes); int row = (w.slot / nl) % Mathf.Max(1, _cfg.walkQueueRows);
            Vector3 goalW;
            if (_cfg.gatherMode == 1 && !w.crossed) goalW = WaitGoal(w);                                   // góp ý 13: chờ trước mép lề
            else if (_cfg.gatherMode == 1) goalW = FlankPoint(w.car) + SlotNose * ((w.order % 3) * _cfg.walkMinDist * 0.8f);   // băng qua đường tới hông trái khung, góc đuôi
            else goalW = BoardPoint(w.car) + SlotNose * (row * _cfg.walkMinDist * 1.05f);   // hàng sau lùi về phía mũi xe / bảng
            var goal = WorldCell(goalW); var from = WorldCell(fromWorld);
            int gyMax = Mathf.Max(goal.y, from.y) + 1;
            int ox, oy; var d = DistField(goal, gyMax, out ox, out oy);
            var pts = BuildPath(from, d, ox, oy, goalW);
            if (pts == null) { pts = new List<Vector3> { fromWorld, goalW }; }   // không có đường: đi thẳng (hiếm)
            pts[0] = fromWorld;
            w.pts = pts; w.cum = new float[pts.Count]; for (int i = 1; i < pts.Count; i++) w.cum[i] = w.cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            w.total = w.cum[pts.Count - 1]; w.dist = 0f; return true;
        }

        /// <summary>Straggler: ô rỗng thông ra ngoài gần nhất để nhảy tới.</summary>
        Vector3 NearestOpenCell(Vector2Int c)
        {
            int ox, oy; var open = OpenField(out ox, out oy); var b = Game.Board;
            Vector2Int best = c; float bd = float.MaxValue;
            for (int x = -1; x <= b.W; x++) for (int y = -1; y <= b.H; y++)
            {
                if (!open[x + ox, y + oy]) continue;
                float dd = (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y);
                if (dd < bd) { bd = dd; best = new Vector2Int(x, y); }
            }
            return CellWorld(best.x, best.y);
        }

        /// <summary>Vị trí thật = vị trí trên đường + lệch làn (vuông góc hướng đi) + lệch do đẩy nhau.</summary>
        Vector3 WPos(Walk w) { return w.pos + Vector3.Cross(Vector3.up, w.dir.sqrMagnitude > 0.001f ? w.dir : Vector3.forward) * (w.lane * LaneFactor(w.pos)) + w.off; }

        /// <summary>Lệch làn chỉ áp dụng ngoài vùng bảng (trong bảng đi theo hành lang 1 ô, lệch sẽ đè lên khối đứng); 0 trong bảng → 1 cách mép 1,5 ô.</summary>
        float LaneFactor(Vector3 p)
        {
            var b = Game.Board; float gx = (p.x - _cellX0) / CellX, gy = (p.z - _boardZ0) / CellZ;
            float dx = Mathf.Max(-gx, gx - (b.W - 1), 0f), dy = Mathf.Max(-gy, gy - (b.H - 1), 0f);
            return Mathf.Clamp01(Mathf.Max(dx, dy) / 1.5f);
        }

        /// <summary>Góp ý 07/08: không lấn vào ô có khối đứng im: coi mỗi ô bị chiếm là hộp, đẩy tâm người ra xa mép hộp ít nhất walkObstacleRadius (2 vòng, 3x3 ô quanh).</summary>
        void KeepOffBlocks(Walk w)
        {
            float r = _cfg.walkObstacleRadius, hx = CellX * 0.5f, hz = CellZ * 0.5f;
            for (int it = 0; it < 2; it++)
            {
                var p = WPos(w); var c = WorldCell(p); bool moved = false;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                {
                    int gx = c.x + dx, gy = c.y + dy; if (!CellOccupied(gx, gy)) continue;
                    var cc = CellWorld(gx, gy);
                    float qx = Mathf.Clamp(p.x, cc.x - hx, cc.x + hx), qz = Mathf.Clamp(p.z, cc.z - hz, cc.z + hz);   // điểm gần nhất trên hộp
                    var d = new Vector3(p.x - qx, 0f, p.z - qz); float dist = d.magnitude;
                    if (dist >= r) continue;
                    if (dist < 0.0001f) { d = p - cc; d.y = 0f; dist = d.magnitude; if (dist < 0.0001f) d = Vector3.forward; d = d.normalized; w.off += d * r; }
                    else w.off += d / dist * (r - dist);
                    moved = true; p = WPos(w);
                }
                if (!moved) break;
            }
        }

        // ---------------------------------------------------------------- mỗi khung
        void TickWalks(float dt)
        {
            // 1) trạng thái + vị trí danh nghĩa
            for (int i = _walks.Count - 1; i >= 0; i--)
            {
                var w = _walks[i]; var tr = w.p.tr; w.moving = false;
                if (Time < w.start) { w.pos = w.p.home; w.off = Vector3.zero; tr.localPosition = w.p.home; continue; }   // chờ tại chỗ (không nhấc, không bay)
                if (w.phase == -1)
                {   // kẹt hẳn (bị màu khác bao kín): nhảy qua ra ô rỗng gần nhất
                    _pendingCells.Remove(w.p.d.X * 1000 + w.p.d.Y);
                    w.hopFrom = w.p.home; w.hopTo = NearestOpenCell(new Vector2Int(w.p.d.X, w.p.d.Y));
                    w.hopDur = Mathf.Max(0.4f, Vector3.Distance(w.hopFrom, w.hopTo) / Mathf.Max(0.1f, w.speed) * 1.5f);
                    w.phase = 1; w.phaseStart = Time; FeelLog("pixel (" + w.p.d.X + "," + w.p.d.Y + ") kẹt: nhảy qua " + Vector3.Distance(w.hopFrom, w.hopTo).ToString("0.0"));
                }
                if (w.phase == 0) { PlanWalk(w, w.p.home); w.phase = 2; w.phaseStart = Time; w.pos = w.p.home; }
                if (w.phase == 1)
                {
                    float u = Mathf.Clamp01((Time - w.phaseStart) / w.hopDur);
                    w.pos = Vector3.Lerp(w.hopFrom, w.hopTo, u) + Vector3.up * (_cfg.stragglerHopHeight * 4f * u * (1f - u));
                    w.dir = w.hopTo - w.hopFrom; w.dir.y = 0f;
                    if (u >= 1f) { PlanWalk(w, w.hopTo); w.phase = 2; w.phaseStart = Time; }
                    continue;
                }
                if (w.phase == 2)
                {
                    // không vượt: có người ngay trước mặt (cùng hướng, gần) thì đi chậm lại
                    float adv = w.speed * dt;
                    foreach (var o in _walks)
                    {
                        if (o == w || (o.phase != 2 && o.phase != 4) || Time < o.start) continue;
                        var dv = WPos(o) - WPos(w); dv.y = 0f; float along = Vector3.Dot(dv, w.dir); float lat = (dv - w.dir * along).magnitude;
                        if (along > 0f && along < _cfg.walkMinDist * 1.1f && lat < _cfg.walkMinDist * 0.6f) { adv *= 0.35f; break; }
                    }
                    w.dist += adv; w.moving = adv > 0.001f;
                    if (w.dist >= w.total) { w.dist = w.total; w.pos = w.pts[w.pts.Count - 1]; w.phase = 4; w.phaseStart = Time; }
                    else
                    {
                        int seg = 1; while (seg < w.cum.Length - 1 && w.cum[seg] < w.dist) seg++;
                        float sl = Mathf.Max(0.0001f, w.cum[seg] - w.cum[seg - 1]); float lu = (w.dist - w.cum[seg - 1]) / sl;
                        w.pos = Vector3.Lerp(w.pts[seg - 1], w.pts[seg], lu);
                        var dir = w.pts[seg] - w.pts[seg - 1]; dir.y = 0f; if (dir.sqrMagnitude > 0.0001f) w.dir = dir.normalized;
                    }
                }
                if (w.phase == 4)
                {   // đứng chờ trước ô bến tới khi xe đỗ xong (góp ý 05), rồi nhảy lên
                    var cv = w.car;
                    bool parked = cv.tr != null && cv.move == null && !cv.leaving;
                    // góp ý 13: đang chờ trước mép lề → xe đỗ xong mới băng qua đường (2–3 ô) tới hông trái khung rồi mới nhảy
                    // góp ý 13 (3c đo clip Lv.2 16,0–16,8 và 20,0–20,6): không bước xuống đường khi CÓ BẤT KỲ xe nào đang chuyển động trong bến (waitWhileBayBusy), kể cả khi xe của mình đã đỗ
                    if (_cfg.gatherMode == 1 && !w.crossed) { if (parked && !(_cfg.waitWhileBayBusy && BayBusy())) { w.crossed = true; PlanWalk(w, WPos(w)); w.phase = 2; w.phaseStart = Time; } continue; }
                    // lên xe LẦN LƯỢT (gốc _boardRockMinInterval): người chờ lâu nhất đi trước, cách nhau boardInterval
                    // W5 (clip 15–25 người / s): tối đa boardParallel người đang nhảy cùng lúc, mỗi người cách boardInterval; người chờ lâu hơn đi trước
                    int ahead = 0, jumping = 0;
                    foreach (var o in _walks) { if (o == w || o.car != cv) continue; if (o.phase == 4 && o.phaseStart < w.phaseStart) ahead++; if (o.phase == 3) jumping++; }
                    if (parked && ahead < _cfg.boardParallel && jumping < _cfg.boardParallel && Time >= cv.lastBoardAt + _cfg.boardInterval) { cv.lastBoardAt = Time; w.phase = 3; w.phaseStart = Time; w.jumpFrom = WPos(w); }
                }
                if (w.phase == 3)
                {   // nhảy lên xe (gốc _jumpHeight / _jumpDuration), thu nhỏ khi vào
                    float u = Mathf.Clamp01((Time - w.phaseStart) / Mathf.Max(0.05f, _cfg.boardJumpDuration));
                    var to = BoardTarget(w.car);   // góp ý 08: vào hông xe, không lên nóc
                    tr.localPosition = Vector3.Lerp(w.jumpFrom, to, u) + Vector3.up * (_cfg.boardJumpHeight * 4f * u * (1f - u));
                    tr.localScale = BlockScale * (_cfg.walkScale * Mathf.Lerp(1f, 0.3f, u * u));
                    if (u >= 1f) { _walks.RemoveAt(i); Kill(tr.gameObject); ForgetPass(w.p); Game.Arrive(w.car.car, w.p.d); }
                }
            }
            // 2) giải va chạm bằng chiếu vị trí (gốc _crowdSolverIterations 3): hai người gần hơn walkMinDist thì đẩy nhau ra mỗi người một nửa (góp ý 06: hết đè)
            float md = _cfg.walkMinDist;
            if (md > 0f)
            {
                var act = new List<Walk>(); foreach (var w in _walks) if ((w.phase == 2 || w.phase == 4) && Time >= w.start) act.Add(w);
                for (int it = 0; it < Mathf.Max(1, _cfg.walkSolverIterations); it++)
                    for (int i = 0; i < act.Count; i++) for (int j = i + 1; j < act.Count; j++)
                    {
                        // khoảng cách đo với chiều sâu (z) nhân walkDepthScale: camera nghiêng nên khối sau bị khối trước che, cần cách xa hơn theo z
                        var a = act[i]; var b = act[j]; var dv = WPos(a) - WPos(b); dv.y = 0f; dv.z *= _cfg.walkDepthScale; float d = dv.magnitude;
                        if (d >= md) continue;
                        if (d < 0.001f) dv = new Vector3(Mathf.Sin(i * 1.7f + j), 0f, Mathf.Cos(i * 1.7f + j)); else dv /= d;
                        float half = (md - d) * 0.5f; var push = new Vector3(dv.x, 0f, dv.z / Mathf.Max(0.5f, _cfg.walkDepthScale)) * half; a.off += push; b.off -= push;
                    }
                foreach (var a in act) { a.off -= a.off * Mathf.Min(1f, _cfg.walkSeparationRelax * dt); if (a.off.magnitude > md * 5f) a.off = a.off.normalized * md * 5f; KeepOffBlocks(a); }
            }
            // 3) áp vào transform: nhún bước khi đang đi, giậm chân nhẹ khi chờ, mặt theo hướng
            foreach (var w in _walks)
            {
                if (Time < w.start || w.phase == 3) continue;
                var tr = w.p.tr;
                if (w.phase == 1) { tr.localPosition = w.pos; if (w.dir.sqrMagnitude > 0.001f) tr.localRotation = Quaternion.LookRotation(w.dir.normalized) * Quaternion.Euler(0f, 180f, 0f); continue; }
                float bob = w.moving ? _cfg.walkBobHeight * Mathf.Abs(Mathf.Sin(w.dist / Mathf.Max(0.05f, _cfg.walkStride) * Mathf.PI))
                                     : _cfg.walkBobHeight * 0.3f * Mathf.Abs(Mathf.Sin((Time - w.phaseStart) * 6f + w.lane * 7f));
                tr.localPosition = WPos(w) + Vector3.up * bob;
                if (w.dir.sqrMagnitude > 0.0001f)
                    tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(w.dir) * Quaternion.Euler(0f, 180f + (w.moving ? _cfg.walkSway * Mathf.Sin(w.dist / Mathf.Max(0.05f, _cfg.walkStride) * Mathf.PI * 2f) : 0f), 0f), 1f - Mathf.Exp(-14f * dt));
                tr.localScale = BlockScale * _cfg.walkScale;   // W1: người đi nhỏ hơn khối đứng (clip: hành khách nhỏ hơn pixel ảnh)
            }
        }
    }
}
