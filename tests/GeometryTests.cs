using System;
using System.IO;
using System.Web.Script.Serialization;
using DragonCodexBoot;

internal static class GeometryTests {
 private static int checks;
 private static void Check(bool ok,string message) { if(!ok)throw new Exception(message);checks++; }
 private static Config Clone(Config c) { var s=new JavaScriptSerializer();return s.Deserialize<Config>(s.Serialize(c)); }
 private static void Reject(Config original,Action<Config> change,string label) {
  Config copy=Clone(original);change(copy);bool rejected=false;
  try { Entry.ValidateConfig(copy); } catch(Exception) { rejected=true; }
  Check(rejected,"Invalid config accepted: "+label);
 }
 public static int Main(string[] args) {
  try {
   Config c=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(args[0]));Entry.ValidateConfig(c);
   Check(!c.AutoReplaceEntrypoints&&!c.ScanAllLocalDrives,"Enhanced public config opts out of automatic integration and drive scanning");
   Check(new Config().AutoReplaceEntrypoints&&new Config().ScanAllLocalDrives,"Missing legacy settings have enabled defaults");
   Check(new Config().StartupCurtain,"Existing configs enable the startup curtain by default");
   Check(!new JavaScriptSerializer().Deserialize<Config>("{\"StartupCurtain\":false}").StartupCurtain,"Curtain diagnostic opt-out is retained");
   string legacy=File.ReadAllText(args[0]).Replace("\"AutoReplaceEntrypoints\": false,","").Replace("\"ScanAllLocalDrives\": false,","");
   Check(new JavaScriptSerializer().Deserialize<Config>(legacy).AutoReplaceEntrypoints,"Legacy JSON retains default");
   Check(!new JavaScriptSerializer().Deserialize<Config>("{\"AutoReplaceEntrypoints\":false}").AutoReplaceEntrypoints,"Explicit opt-out is retained");
   Check(Geometry.At(c.ScreenFrames,-1)==c.ScreenFrames[0],"First frame clamp");
   Check(Geometry.At(c.ScreenFrames,999)==c.ScreenFrames[c.ScreenFrames.Count-1],"Last frame clamp");
   double mid=(c.ScreenFrames[0].Time+c.ScreenFrames[1].Time)/2;
   var frame=Geometry.At(c.ScreenFrames,mid);
   Check(Math.Abs(frame.X-(c.ScreenFrames[0].X+c.ScreenFrames[1].X)/2)<1e-10,"Interpolated x");
   Bounds monitor=new Bounds(-2560,-1440,2560,1440);
   Bounds centered=Geometry.Centered(monitor,1920,1080);
   Check(centered.Left==-2240&&centered.Top==-1260&&centered.Width==1920&&centered.Height==1080,"Negative monitor center");
   Bounds full=Geometry.Dest(c.ScreenFrames,c.TransitionEnd,1200,900,1);
   Check(full.Left==0&&full.Top==0&&full.Right==1200&&full.Bottom==900,"Final non-16:9 viewport fills");
   Bounds measured=Geometry.Dest(c.ScreenFrames,13,1440,810,.5);
   Check(measured.Left==(int)Math.Round(.074*1440)&&measured.Top==(int)Math.Round(.061*810),"16:9 keyframe position must not drift during fade");
   Check(measured.Width==(int)Math.Round(.845*1440)&&measured.Height==(int)Math.Round(.825*810),"16:9 keyframe size must not expand twice");
   Bounds fadeStart=Geometry.Dest(c.ScreenFrames,13,1440,810,0);
   Bounds fadeEnd=Geometry.Dest(c.ScreenFrames,13,1440,810,1);
   Check(fadeStart.Left==fadeEnd.Left&&fadeStart.Width==fadeEnd.Width,"16:9 screen tracking is independent from opacity");
   foreach(Bounds viewport in new [] { new Bounds(0,0,1920,1080),new Bounds(-2560,0,2560,1440),
    new Bounds(1920,0,1920,1040),new Bounds(0,-1920,1080,1920),new Bounds(0,0,1280,1024),new Bounds(0,0,3440,1440) }) {
    Bounds video=Geometry.FitVideo(viewport);
    Check(Math.Abs(video.Width/1920.0-video.Height/1080.0)<.001,"Video must preserve 16:9, including portrait and ultrawide screens");
    Check(video.Left>=viewport.Left&&video.Top>=viewport.Top&&video.Right<=viewport.Right&&video.Bottom<=viewport.Bottom,"Complete video fits viewport without crop");
    Check(Math.Abs(video.Left-viewport.Left-(viewport.Width-video.Width)/2)<=1,"Video is centered");
    Bounds end=Geometry.Dest(c.ScreenFrames,c.TransitionEnd,viewport.Width,viewport.Height,1);
    Check(end.Width==viewport.Width&&end.Height==viewport.Height,"Actual UI fills client after transition");
   }
   Bounds small=Geometry.FitPlayer(new Bounds(-1280,0,1280,720),1440,810);
   Check(small.Left>=-1280&&small.Right<=0&&small.Top>=0&&small.Bottom<=720,"Fallback player fits small negative-coordinate monitor");
   Config follow=Clone(c);follow.FollowClientWindow=true;follow.MatchClientToPlayer=false;Entry.ValidateConfig(follow);
   Check(follow.FollowClientWindow&&!follow.MatchClientToPlayer,"Follow mode does not resize Codex");
   StartGate gate=new StartGate();
   for(int n=0;n<10000;n++)gate.Step();
   Check(gate.Frame==0&&!gate.Accepted,"Idle gate must not auto-start after waiting");
   gate.Hover(true);for(int n=0;n<10;n++)gate.Step();
   Check(gate.Frame==StartGate.HoverFrame&&!gate.Accepted,"Hover points hand without clicking");
   gate.Hover(false);for(int n=0;n<10;n++)gate.Step();
   Check(gate.Frame==0&&!gate.Accepted,"Leaving returns hand to idle");
   gate.Press();gate.Hover(false);gate.CancelPress();for(int n=0;n<10;n++)gate.Step();
   Check(!gate.Accepted&&gate.Frame==0,"Press and drag outside must cancel");
   gate.Commit();gate.Commit();for(int n=0;n<10;n++)gate.Step();
   Check(gate.Accepted&&gate.Frame==StartGate.PressFrame,"Click reaches actual pressed pose before accepting");
   gate.Hover(false);gate.Step();Check(gate.Accepted&&gate.Frame==StartGate.PressFrame,"Accepted gate is one-shot");
   WindowReadyGate windowGate=new WindowReadyGate();
   Bounds oldClient=new Bounds(50,60,900,600),newClient=new Bounds(1920,0,1920,1020);
   Check(!windowGate.Observe(IntPtr.Zero,oldClient,false,0),"A background process with no visible window must not start an animation");
   Check(!windowGate.Observe(new IntPtr(1),oldClient,true,.1),"First visible sample does not imply stable geometry");
   Check(!windowGate.Observe(new IntPtr(1),oldClient,false,.3),"Closing or hiding a window invalidates old geometry");
   Check(!windowGate.Observe(new IntPtr(1),oldClient,true,.4),"Reopening same hidden HWND resets stability");
   Check(!windowGate.Observe(new IntPtr(1),newClient,true,.6),"Restored resize resets stability");
   Check(!windowGate.Observe(new IntPtr(1),newClient,true,.8),"Do not start on a transient restored size");
   Check(windowGate.Observe(new IntPtr(1),newClient,true,1.01),"Stable visible restored geometry is accepted");
   Check(!windowGate.Observe(new IntPtr(2),newClient,true,1.1),"Recreated window HWND invalidates previous ready state");
   Check(windowGate.Observe(new IntPtr(2),newClient,true,1.51),"New replacement window can become ready");
   Check(!windowGate.Observe(new IntPtr(2),new Bounds(0,0,0,0),false,1.6),"Minimized zero client cannot pass preflight");
   Check(!windowGate.Observe(new IntPtr(2),newClient,true,1.7),"Minimized restore needs a fresh stable period");
   Check(windowGate.Observe(new IntPtr(2),newClient,true,2.11),"Minimized restore ultimately passes");
   Bounds oldOuter=new Bounds(100,100,1016,739),oldInner=new Bounds(108,131,1000,700);
   Bounds restoredClient=Geometry.ClientForOuter(new Bounds(2000,50,1216,839),oldOuter,oldInner,1);
   Check(restoredClient.Left==2008&&restoredClient.Top==81&&restoredClient.Width==1200&&restoredClient.Height==800,"Restored outer/client insets preserve the window-sized cover");
   Bounds dpiClient=Geometry.ClientForOuter(new Bounds(-1920,0,1524,1000),oldOuter,oldInner,1.5);
   Check(dpiClient.Left==-1908&&dpiClient.Top==46&&dpiClient.Width==1500,"Restored insets scale across DPI");
   string predictionSource;Bounds noGuess=ClientPlacementMemory.Predict(c,IntPtr.Zero,null,out predictionSource);
   Check(noGuess.Width==0&&noGuess.Height==0,"Unknown window must not become a full-screen curtain");
   var staleMax=new ClientPlacementMemory {AppLaunch=c.AppLaunch,Client=new Bounds(0,0,1920,1080),Outer=new Bounds(0,0,1920,1080),Maximized=true};
   Check(ClientPlacementMemory.Predict(c,IntPtr.Zero,staleMax,out predictionSource).Width==0,"A stale maximized cache must not trigger a full-screen white fallback");
   Config interactive=Clone(c);interactive.InteractiveStart=true;Entry.ValidateConfig(interactive);
   Check(interactive.InteractiveStart,"Interactive start accepts the bundled timing");
   Reject(interactive,x=>x.HoldAt=.6,"Hold node before hand press completes");
   Reject(c,x=>x.TransitionEnd=x.TransitionStart,"zero fade length");
   Reject(c,x=>x.HoldAt=x.TransitionStart,"hold after fade");
   Reject(c,x=>x.Volume=Double.NaN,"NaN volume");
   Reject(c,x=>x.Volume=1.01,"volume range");
   Reject(c,x=>x.TransitionStart=Double.PositiveInfinity,"infinite time");
   Reject(c,x=>x.ScreenFrames[0].X=Double.NaN,"NaN position");
   Reject(c,x=>x.ScreenFrames[0].Width=0,"zero screen width");
   Reject(c,x=>x.ScreenFrames[0].Time=-1,"negative keyframe time");
   Reject(c,x=>x.ScreenFrames[1].Time=x.ScreenFrames[0].Time,"duplicate keyframe time");
   Reject(c,x=>x.ScreenFrames[0]=null,"null keyframe");
   Reject(c,x=>x.ProcessNames[0]=" ","blank process");
   Reject(c,x=>x.AppLaunch="shell:AppsFolder\\app!App extra","shell argument injection");
   Reject(c,x=>x.PlayerWidth=10,"unusable player width");
   Console.WriteLine("PASS: "+checks+" geometry/configuration assertions. No desktop UI opened.");
   return 0;
  } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
 }
}
