using System; using System.IO; using System.Text; using System.Globalization; using SleepingCalamity.Core;
static class P {
  static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
  static void Seg(StringBuilder sb, TownLayout t, System.Collections.Generic.List<(TownPoint A, TownPoint B)> run) {
    for (int i=0;i<run.Count;i++){ if(i>0)sb.Append(','); var a=run[i].A; var b=run[i].B; sb.Append($"[{F(a.X)},{F(a.Z)},{F(b.X)},{F(b.Z)}]"); }
  }
  static int Main(string[] a) {
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    var t = HollowmereLayout.Create();
    var errs = t.Validate();
    foreach (var e in errs) Console.WriteLine("ERR " + e);
    for (int r=1;r<=3;r++) Console.WriteLine($"ring {r}: S={t.Count(r,FoundationSize.S)} M={t.Count(r,FoundationSize.M)} L={t.Count(r,FoundationSize.L)}");
    Console.WriteLine($"nodes={t.Nodes.Count} edges={t.Edges.Count} errors={errs.Count}");
    if (a.Length > 0) {
      var sb = new StringBuilder("{\"foundations\":[");
      for (int i=0;i<t.Foundations.Count;i++){ var f=t.Foundations[i]; if(i>0)sb.Append(','); 
        sb.Append($"{{\"id\":\"{f.Id}\",\"ring\":{f.Ring},\"size\":\"{f.Size}\",\"side\":{F(f.Side)},\"x\":{F(f.Centre.X)},\"z\":{F(f.Centre.Z)},\"yaw\":{F(f.Yaw)},\"facing\":\"{f.Facing}\",\"facingYaw\":{F(f.FacingYaw)},\"y\":{F(f.Y)},\"ruin\":\"{f.RuinVariant}\",\"rebuilt\":\"{HollowmereLayout.RebuiltBuilding(f.Id)}\"}}"); }
      sb.Append("],\"landmarks\":[");
      for (int i=0;i<t.Landmarks.Count;i++){ var l=t.Landmarks[i]; if(i>0)sb.Append(',');
        sb.Append($"{{\"id\":\"{l.Id}\",\"name\":\"{l.Name}\",\"kind\":\"{l.Kind}\",\"x\":{F(l.Footprint.Centre.X)},\"z\":{F(l.Footprint.Centre.Z)},\"sx\":{F(l.Footprint.HalfX*2)},\"sz\":{F(l.Footprint.HalfZ*2)},\"yaw\":{F(l.Footprint.Yaw)},\"y\":{F(l.Y)},\"h\":{F(l.Height)}}}"); }
      sb.Append("],\"lanes\":[");
      for (int i=0;i<t.Lanes.Count;i++){ var l=t.Lanes[i]; if(i>0)sb.Append(','); sb.Append($"{{\"id\":\"{l.Id}\",\"name\":\"{l.Name}\",\"ring\":{l.Ring},\"width\":{F(l.Width)},\"pts\":[");
        for(int k=0;k<l.Points.Length;k++){ if(k>0)sb.Append(','); var p=l.Points[k]; sb.Append($"[{F(p.X)},{F(p.Z)},{F(HollowmereLayout.Ground(t,p.X,p.Z))}]"); } sb.Append("]}"); }
      sb.Append("],\"nodes\":[");
      for (int i=0;i<t.Nodes.Count;i++){ var n=t.Nodes[i]; if(i>0)sb.Append(','); sb.Append($"{{\"id\":\"{n.Id}\",\"x\":{F(n.Pos.X)},\"z\":{F(n.Pos.Z)},\"y\":{F(n.Y)},\"kind\":\"{n.Kind}\"}}"); }
      sb.Append("],\"edges\":[");
      for (int i=0;i<t.Edges.Count;i++){ var e=t.Edges[i]; if(i>0)sb.Append(','); sb.Append($"[\"{e.A}\",\"{e.B}\"]"); }
      sb.Append("],\"stream\":[");
      for (int i=0;i<HollowmereLayout.Stream.Length;i++){ if(i>0)sb.Append(','); var p=HollowmereLayout.Stream[i]; sb.Append($"[{F(p.X)},{F(p.Z)}]"); }
      sb.Append("],\"wall\":[");
      Seg(sb, t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TownWallOffset, 16f), 0.5f));
      sb.Append("],\"terrace\":[");
      Seg(sb, t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TerraceOffset, 16f), 1.0f));
      sb.Append("],\"keepwall\":[");
      Seg(sb, t, HollowmereLayout.WallRun(t, HollowmereLayout.KeepWallLine(), 0.3f));
      sb.Append($"],\"waterY\":{F(HollowmereLayout.WaterY)},\"keepWallZ\":{F(HollowmereLayout.KeepWallZ)},\"keepHalfWidth\":{F(HollowmereLayout.KeepHalfWidth)},\"keepHeight\":{F(HollowmereLayout.KeepHeight)},");
      // heightmap 2 m over x -140..140, z -160..100
      int nx=141, nz=131; sb.Append($"\"hm\":{{\"x0\":-140,\"z0\":-160,\"step\":2,\"nx\":{nx},\"nz\":{nz},\"h\":[");
      for(int j=0;j<nz;j++) for(int i=0;i<nx;i++){ if(i+j>0)sb.Append(','); sb.Append(F(HollowmereLayout.Ground(t,-140+2*i,-160+2*j))); }
      sb.Append("]}}");
      File.WriteAllText(a[0], sb.ToString());
    }
    return errs.Count == 0 ? 0 : 1;
  }
}
