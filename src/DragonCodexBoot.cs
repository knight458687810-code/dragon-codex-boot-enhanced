using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Windows.Threading;

[assembly: AssemblyTitle("Dragon Codex Boot")]
[assembly: AssemblyProduct("Dragon Codex Boot")]
[assembly: AssemblyDescription("Community animation launcher for the Codex desktop client")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]

namespace DragonCodexBoot {
 public sealed class Config {
  public Config() { AutoReplaceEntrypoints=true; ScanAllLocalDrives=true; StartupCurtain=true; }
  public bool AutoReplaceEntrypoints { get; set; }
  public bool ScanAllLocalDrives { get; set; }
  public string DisplayName { get; set; }
  public string Video { get; set; }
  public string AppLaunch { get; set; }
  public string ProcessPathContains { get; set; }
  public string[] ProcessNames { get; set; }
  public double HoldAt { get; set; }
  public double TransitionStart { get; set; }
  public double TransitionEnd { get; set; }
  public double MaxWaitSeconds { get; set; }
  public double Volume { get; set; }
  public int PlayerWidth { get; set; }
  public int PlayerHeight { get; set; }
  public bool MatchClientToPlayer { get; set; }
  public bool FollowClientWindow { get; set; }
  public bool InteractiveStart { get; set; }
  public bool StartupCurtain { get; set; }
  public List<ScreenFrame> ScreenFrames { get; set; }
 }
 public sealed class WindowReadyGate {
  private IntPtr last;private Bounds bounds;private double stableAt=-1;
  public bool Observe(IntPtr handle,Bounds current,bool usable,double now) {
   if(!usable||handle==IntPtr.Zero||current.Width<450||current.Height<280) {
    last=IntPtr.Zero;stableAt=-1;return false;
   }
   if(last!=handle||bounds.Left!=current.Left||bounds.Top!=current.Top
    ||bounds.Width!=current.Width||bounds.Height!=current.Height||stableAt<0) {
    last=handle;bounds=current;stableAt=now;return false;
   }
   return now-stableAt>=.4;
  }
 }
 public sealed class StartGate {
  public const double IdleTime=.5,PressTime=.875;
  public const int HoverFrame=8,PressFrame=9;
  public int Frame { get; private set; }
  public int TargetFrame { get; private set; }
  public bool Accepted { get; private set; }
  public bool CommitRequested { get; private set; }
  private bool hovered,pressed;
  public void Hover(bool value) { hovered=value;if(!pressed&&!CommitRequested)TargetFrame=value?HoverFrame:0; }
  public void Press() { if(Accepted||CommitRequested)return;pressed=true;TargetFrame=PressFrame; }
  public void CancelPress() { if(CommitRequested)return;pressed=false;TargetFrame=hovered?HoverFrame:0; }
  public void Commit() { if(Accepted||CommitRequested)return;CommitRequested=true;pressed=true;TargetFrame=PressFrame; }
  public void Step() {
   if(Accepted)return;
   if(Frame<TargetFrame)Frame++;else if(Frame>TargetFrame)Frame--;
   if(CommitRequested&&Frame==PressFrame)Accepted=true;
  }
 }
 public sealed class ScreenFrame {
  public double Time { get; set; }
  public double X { get; set; }
  public double Y { get; set; }
  public double Width { get; set; }
  public double Height { get; set; }
 }
 [StructLayout(LayoutKind.Sequential)]
 public struct Bounds {
  public int Left,Top,Right,Bottom;
  public int Width { get { return Right-Left; } }
  public int Height { get { return Bottom-Top; } }
  public Bounds(int x,int y,int w,int h) { Left=x;Top=y;Right=x+w;Bottom=y+h; }
 }
 public static class Geometry {
  public static double Clamp(double x) { return Math.Max(0,Math.Min(1,x)); }
  public static double Ease(double x) { x=Clamp(x);return x*x*(3-2*x); }
  public static Bounds Lerp(Bounds a,Bounds b,double t) {
   return new Bounds((int)Math.Round(a.Left+(b.Left-a.Left)*t),(int)Math.Round(a.Top+(b.Top-a.Top)*t),
    (int)Math.Round(a.Width+(b.Width-a.Width)*t),(int)Math.Round(a.Height+(b.Height-a.Height)*t));
  }
  public static Bounds Centered(Bounds monitor,int width,int height) {
   // These are physical pixels, independent of Windows display scaling.
   return new Bounds(monitor.Left+(monitor.Width-width)/2,monitor.Top+(monitor.Height-height)/2,width,height);
  }
  public static Bounds FitVideo(Bounds viewport) {
   double scale=Math.Min(viewport.Width/1920.0,viewport.Height/1080.0);
   int width=Math.Max(1,(int)Math.Round(1920*scale));
   int height=Math.Max(1,(int)Math.Round(1080*scale));
   return Centered(viewport,width,height);
  }
  public static Bounds FitPlayer(Bounds workArea,int preferredWidth,int preferredHeight) {
   double scale=Math.Min(1,Math.Min(workArea.Width/(double)preferredWidth,workArea.Height/(double)preferredHeight));
   return Centered(workArea,Math.Max(1,(int)Math.Floor(preferredWidth*scale)),Math.Max(1,(int)Math.Floor(preferredHeight*scale)));
  }
  public static Bounds ClientForOuter(Bounds outer,Bounds oldOuter,Bounds oldClient,double scale) {
   int left=(int)Math.Round((oldClient.Left-oldOuter.Left)*scale),top=(int)Math.Round((oldClient.Top-oldOuter.Top)*scale);
   int right=(int)Math.Round((oldOuter.Right-oldClient.Right)*scale),bottom=(int)Math.Round((oldOuter.Bottom-oldClient.Bottom)*scale);
   return new Bounds(outer.Left+left,outer.Top+top,Math.Max(1,outer.Width-left-right),Math.Max(1,outer.Height-top-bottom));
  }
  public static Bounds WindowForClient(Bounds window,Bounds client,Bounds desired) {
   return new Bounds(desired.Left-(client.Left-window.Left),desired.Top-(client.Top-window.Top),
    desired.Width+window.Width-client.Width,desired.Height+window.Height-client.Height);
  }
  public static ScreenFrame At(List<ScreenFrame> frames,double t) {
   if(t<=frames[0].Time)return frames[0];
   for(int i=1;i<frames.Count;i++) {
    if(t>frames[i].Time)continue;
    ScreenFrame a=frames[i-1],b=frames[i];
    double u=Clamp((t-a.Time)/(b.Time-a.Time));
    return new ScreenFrame { Time=t,X=a.X+(b.X-a.X)*u,Y=a.Y+(b.Y-a.Y)*u,
     Width=a.Width+(b.Width-a.Width)*u,Height=a.Height+(b.Height-a.Height)*u };
   }
   return frames[frames.Count-1];
  }
  public static Bounds Dest(List<ScreenFrame> frames,double time,int w,int h,double fade) {
   ScreenFrame r=At(frames,time);
   // The video is fitted without distorting its 16:9 character drawing.
   Bounds fitted=FitVideo(new Bounds(0,0,w,h));
   double vw=fitted.Width,vh=fitted.Height,ox=fitted.Left,oy=fitted.Top;
   Bounds inVideo=new Bounds((int)Math.Round(ox+r.X*vw),(int)Math.Round(oy+r.Y*vh),
    Math.Max(1,(int)Math.Round(r.Width*vw)),Math.Max(1,(int)Math.Round(r.Height*vh)));
   // Remove any letterboxing as the actual software fills the target window.
   // Native 16:9 playback already matches the measured screen keyframes.
   // A second expansion here drifts outside the animated screen before it fills the frame.
   if((long)w*1080==(long)h*1920)return inVideo;
   return Lerp(inVideo,new Bounds(0,0,w,h),Ease(fade));
  }
 }
 internal static class Native {
  public delegate bool EnumWindowsProc(IntPtr h,IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] public struct WindowPlacement {
   public int Length,Flags,ShowCmd;public Point MinPosition,MaxPosition;public Bounds NormalPosition;
  }
  [StructLayout(LayoutKind.Sequential)] public struct WindowPos {
   public IntPtr Window,InsertAfter; public int X,Y,Width,Height; public uint Flags;
  }
  [StructLayout(LayoutKind.Sequential)] public struct ThumbProperties {
   public uint Flags; public Bounds Destination; public Bounds Source;
   public byte Opacity;
   [MarshalAs(UnmanagedType.Bool)] public bool Visible;
   [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
  }
  public delegate void WinEventProc(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time);
  [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
  [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
  public static uint WindowDpi(IntPtr h) { try { uint value=GetDpiForWindow(h);return value==0?96:value; } catch {return 96;} }
  public static bool MatchesProcess(Config cfg,uint pid) {
   IntPtr h=OpenProcess(0x1000,false,(int)pid);if(h==IntPtr.Zero)return false;
   try { StringBuilder path=new StringBuilder(4096);int length=path.Capacity;
    if(!QueryFullProcessImageName(h,0,path,ref length))return false;
    string text=path.ToString();return text.IndexOf(cfg.ProcessPathContains,StringComparison.OrdinalIgnoreCase)>=0
     &&cfg.ProcessNames.Any(name=>String.Equals(name,Path.GetFileNameWithoutExtension(text),StringComparison.OrdinalIgnoreCase));
   } finally {CloseHandle(h);}
  }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb,IntPtr param);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Bounds b);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Bounds b);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref Point p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint cmd);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int how);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int how);
  [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h,ref WindowPlacement placement);
  public static bool RestoreApp(IntPtr h) {
   WindowPlacement placement=new WindowPlacement();placement.Length=Marshal.SizeOf(typeof(WindowPlacement));
   bool maximized=GetWindowPlacement(h,ref placement)&&(placement.Flags&2)!=0;
   return ShowWindowAsync(h,maximized?3:9);
  }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
  [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint ms,out IntPtr result);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window,int id);
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder path,ref int length);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmFlush();
  [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest,IntPtr source,out IntPtr thumb);
  [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumb);
  [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumb,ref ThumbProperties props);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int val,int size);
  public static Bounds ClientBounds(IntPtr h) {
   Bounds r;Point p=new Point();
   if(!GetClientRect(h,out r)||!ClientToScreen(h,ref p))return new Bounds();
   return new Bounds(p.X,p.Y,r.Width,r.Height);
  }
  public static bool Responsive(IntPtr h) {
   IntPtr result;
   return h!=IntPtr.Zero&&SendMessageTimeout(h,0,IntPtr.Zero,IntPtr.Zero,2,80,out result)!=IntPtr.Zero;
  }
  public static bool AlignClient(IntPtr h,Bounds desired) {
   if(IsIconic(h)||IsZoomed(h))ShowWindow(h,9);
   // Measure again after resizing: a restored Chromium window can change frame insets.
   for(int attempt=0;attempt<2;attempt++) {
    Bounds window,client=ClientBounds(h);
    if(!GetWindowRect(h,out window)||client.Width<=0||client.Height<=0)return false;
    Bounds outer=Geometry.WindowForClient(window,client,desired);
    if(!SetWindowPos(h,IntPtr.Zero,outer.Left,outer.Top,outer.Width,outer.Height,0x4|0x10|0x200))return false;
   }
   Bounds actual=ClientBounds(h);
   return Math.Abs(actual.Left-desired.Left)<=2&&Math.Abs(actual.Top-desired.Top)<=2
    &&Math.Abs(actual.Width-desired.Width)<=2&&Math.Abs(actual.Height-desired.Height)<=2;
  }
  public static IntPtr FindApp(Config cfg) { return FindApp(cfg,false); }
  public static IntPtr FindApp(Config cfg,bool includeHidden) {
   HashSet<int> pids=new HashSet<int>();
   foreach(string name in cfg.ProcessNames)foreach(Process p in Process.GetProcessesByName(name)) {
    try {
     string path="";
     IntPtr process=OpenProcess(0x1000,false,p.Id);
     if(process!=IntPtr.Zero) {
      try { StringBuilder buffer=new StringBuilder(4096);int length=buffer.Capacity;if(QueryFullProcessImageName(process,0,buffer,ref length))path=buffer.ToString(); }
      finally { CloseHandle(process); }
     }
     if(path.IndexOf(cfg.ProcessPathContains,StringComparison.OrdinalIgnoreCase)>=0)pids.Add(p.Id);
    } catch {} finally { p.Dispose(); }
   }
   IntPtr found=IntPtr.Zero;
   EnumWindows(delegate(IntPtr h,IntPtr unused) {
    uint pid;GetWindowThreadProcessId(h,out pid);
    if(!pids.Contains((int)pid)||(!includeHidden&&!IsWindowVisible(h))||GetWindow(h,4)!=IntPtr.Zero||(GetWindowLong(h,-20)&0x80)!=0)return true;
    int cloaked;
    if(!includeHidden&&DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked!=0)return true;
    // A minimized top-level window can report a zero-sized client area.
    // It is an existing client, not a cold start. Restore and measure it before playback.
    if(IsIconic(h)) { found=h;return false; }
    Bounds b=ClientBounds(h);
    if(b.Width<450||b.Height<280)return true;
    found=h;return false; // Frontmost eligible main window; no title or screenshot is recorded.
   },IntPtr.Zero);
   return found;
  }
 }
 public sealed class ClientPlacementMemory {
  public string AppLaunch {get;set;}
  public Bounds Client {get;set;}
  public Bounds Outer {get;set;}
  public Bounds WorkArea {get;set;}
  public uint Dpi {get;set;}
  public bool Maximized {get;set;}
  public static bool Valid(Bounds b) {return b.Width>=450&&b.Height>=280&&b.Width<=16384&&b.Height<=16384&&Math.Abs((long)b.Left)<100000&&Math.Abs((long)b.Top)<100000;}
  public static ClientPlacementMemory Load(Config cfg,string root) {
   try { var value=new JavaScriptSerializer().Deserialize<ClientPlacementMemory>(File.ReadAllText(Path.Combine(root,".integration","last-client-window.json")));
    return value!=null&&value.AppLaunch==cfg.AppLaunch&&Valid(value.Client)&&Valid(value.Outer)?value:null;
   } catch { return null; }
  }
  public static void Remember(Config cfg,string root,IntPtr window) {
   try {
    if(window==IntPtr.Zero||!Native.IsWindow(window)||Native.IsIconic(window))return;
    Bounds outer,client=Native.ClientBounds(window);if(!Valid(client)||!Native.GetWindowRect(window,out outer)||!Valid(outer))return;
    var work=System.Windows.Forms.Screen.FromHandle(window).WorkingArea;
    var value=new ClientPlacementMemory { AppLaunch=cfg.AppLaunch,Client=client,Outer=outer,
     WorkArea=new Bounds(work.Left,work.Top,work.Width,work.Height),Dpi=Native.WindowDpi(window),Maximized=Native.IsZoomed(window) };
    string dir=Path.Combine(root,".integration");Directory.CreateDirectory(dir);
    string path=Path.Combine(dir,"last-client-window.json"),temp=path+".tmp";
    File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value));
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   } catch {}
  }
  public static Bounds Predict(Config cfg,IntPtr window,ClientPlacementMemory memory,out string source) {
   source="unknown-no-cover";
   if(window!=IntPtr.Zero&&Native.IsWindow(window)) {
    if(!Native.IsIconic(window)) {
     Bounds live=Native.ClientBounds(window);if(Valid(live)) {source="live-window";return live;}
    } else {
     Native.WindowPlacement place=new Native.WindowPlacement();place.Length=Marshal.SizeOf(typeof(Native.WindowPlacement));
     if(Native.GetWindowPlacement(window,ref place)&&Valid(place.NormalPosition)) {
      var normal=place.NormalPosition;var screen=System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(normal.Left,normal.Top,normal.Width,normal.Height));
      var work=screen.WorkingArea;var full=screen.Bounds;
      if((Native.GetWindowLong(window,-20)&0x80)==0)normal=new Bounds(normal.Left+work.Left-full.Left,normal.Top+work.Top-full.Top,normal.Width,normal.Height);
      double scale=memory!=null&&memory.Dpi>0?Native.WindowDpi(window)/(double)memory.Dpi:1;
      if((place.Flags&2)!=0) {
       source="restore-maximized";
       if(memory!=null&&memory.Maximized) {
        int l=(int)Math.Round((memory.Client.Left-memory.WorkArea.Left)*scale),t=(int)Math.Round((memory.Client.Top-memory.WorkArea.Top)*scale);
        int rr=(int)Math.Round((memory.WorkArea.Right-memory.Client.Right)*scale),bb=(int)Math.Round((memory.WorkArea.Bottom-memory.Client.Bottom)*scale);
        return new Bounds(work.Left+l,work.Top+t,work.Width-l-rr,work.Height-t-bb);
       }
       int caption=memory==null?0:(int)Math.Round(Math.Max(0,memory.Client.Top-memory.Outer.Top-(memory.Client.Left-memory.Outer.Left))*scale);
       return new Bounds(work.Left,work.Top+caption,work.Width,work.Height-caption);
      }
      if(memory!=null) {source="restore-normal";return Geometry.ClientForOuter(normal,memory.Outer,memory.Client,scale);}
     }
    }
   }
   if(memory!=null&&!memory.Maximized) {
    Bounds last=memory.Client;
    foreach(var screen in System.Windows.Forms.Screen.AllScreens) {
     var area=screen.Bounds;
     if(last.Left<area.Right&&last.Right>area.Left&&last.Top<area.Bottom&&last.Bottom>area.Top) {source="last-client-window";return last;}
    }
   }
   // No reliable size (or only a stale maximized snapshot): wait for the actual HWND.
   // Never substitute any monitor rectangle merely because the app is closed.
   return new Bounds();
  }
 }
 internal sealed class StartupCurtains:IDisposable {
  private Window cover;private Bounds area;private bool shown,disposed;private IntPtr trackedWindow;
  private readonly Config cfg;private readonly string root;private readonly ClientPlacementMemory memory;
  private Native.WinEventProc callback;private IntPtr showHook,moveHook;private readonly HashSet<uint> approved=new HashSet<uint>();
  public int Count {get{return cover==null?0:1;}}
  public BitmapSource FirstFrame {get;private set;}
  private void Log(string line) {try {Directory.CreateDirectory(Path.Combine(root,"logs"));File.AppendAllText(Path.Combine(root,"logs","launcher.log"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+line+Environment.NewLine);}catch{}}
  public StartupCurtains(Config c,string baseDir) {
   cfg=c;root=baseDir;memory=ClientPlacementMemory.Load(cfg,root);
   BitmapImage first=new BitmapImage();first.BeginInit();first.CacheOption=BitmapCacheOption.OnLoad;
   first.UriSource=new Uri(Path.Combine(root,"assets","startup-first-frame.png"));first.EndInit();first.Freeze();FirstFrame=first;
   IntPtr known=Native.FindApp(cfg);if(known==IntPtr.Zero)known=Native.FindApp(cfg,true);
   MatchWindow(known);
   callback=delegate(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time) {
    if(disposed||window==IntPtr.Zero||objectId!=0||childId!=0||Native.GetWindow(window,4)!=IntPtr.Zero||(Native.GetWindowLong(window,-20)&0x80)!=0)return;
    uint pid;Native.GetWindowThreadProcessId(window,out pid);
    if(!approved.Contains(pid)) {if(!Native.MatchesProcess(cfg,pid))return;approved.Add(pid);}
    if(!Native.IsWindowVisible(window)||Native.IsIconic(window))return;
    Apply(Native.ClientBounds(window),"actual-window-event");
   };
   // Short-lived out-of-context notifications only; no DLL injection or resident watcher.
   showHook=Native.SetWinEventHook(0x8002,0x8002,IntPtr.Zero,callback,0,0,2);
   moveHook=Native.SetWinEventHook(0x800B,0x800B,IntPtr.Zero,callback,0,0,2);
  }
  public void MatchWindow(IntPtr target) {if(target!=IntPtr.Zero)trackedWindow=target;string source;Apply(ClientPlacementMemory.Predict(cfg,target,memory,out source),source);}
  private IntPtr LockBounds(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
   if(message==0x0046&&!disposed&&l!=IntPtr.Zero) {
    Native.WindowPos pos=(Native.WindowPos)Marshal.PtrToStructure(l,typeof(Native.WindowPos));pos.X=area.Left;pos.Y=area.Top;pos.Width=area.Width;pos.Height=area.Height;pos.Flags&=~(uint)3;
    Marshal.StructureToPtr(pos,l,false);handled=true;
   }return IntPtr.Zero;
  }
  public void Apply(Bounds next,string source) {
   if(disposed||!ClientPlacementMemory.Valid(next))return;
   if(cover!=null&&next.Left==area.Left&&next.Top==area.Top&&next.Width==area.Width&&next.Height==area.Height)return;
   area=next;
   if(cover==null) {
    cover=new Window {Title="Dragon Codex Boot preparation",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,
     Background=new SolidColorBrush(Color.FromRgb(250,247,253)),WindowStartupLocation=WindowStartupLocation.Manual,
     Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,Content=new Image {Source=FirstFrame,Stretch=Stretch.Uniform}};
    cover.SourceInitialized+=delegate {IntPtr h=new WindowInteropHelper(cover).Handle;HwndSource.FromHwnd(h).AddHook(LockBounds);Native.SetWindowPos(h,new IntPtr(-1),area.Left,area.Top,area.Width,area.Height,0x10);};
    if(shown)cover.Show();
   } else {IntPtr h=new WindowInteropHelper(cover).Handle;if(h!=IntPtr.Zero)Native.SetWindowPos(h,new IntPtr(-1),area.Left,area.Top,area.Width,area.Height,0x10);}
   Log("startup-curtain-scoped source="+source+" client="+area.Width+"x"+area.Height+" at "+area.Left+","+area.Top);
  }
  public void Show() {shown=true;if(cover!=null)cover.Show();else Log("startup-curtain-awaiting-actual-window no-screen-fallback");}
  public void Dispose() {
   if(disposed)return;disposed=true;
   if(showHook!=IntPtr.Zero)Native.UnhookWinEvent(showHook);if(moveHook!=IntPtr.Zero)Native.UnhookWinEvent(moveHook);
   if(cover!=null&&cover.IsVisible){cover.Content=null;cover.Close();}cover=null;FirstFrame=null;callback=null;
  }
 }
 public sealed class BootWindow:Window {
  private readonly Config cfg;
  private readonly string root;
  private readonly MediaElement media;
  private readonly TextBlock status;
  private readonly DispatcherTimer timer;
  private readonly Stopwatch wall=new Stopwatch();
  private readonly Action firstSurfaceReady;
  private Image openingStill;
  private IntPtr host,target,thumbnail,stableHandle,alignedHandle;
  private Bounds initialBounds;
  private double stableSince,lastProbe=-1,lastAlign=-1,lastFollow=-1,lastFollowLog=-1;
  private bool probePending;
  private int fullSearches;
  private int alignAttempts;
  private bool ready,opened,holding,closing,startedFade,thumbnailFailed;
  private double endedAt=-1,targetLostAt=-1;
  private readonly StartGate startGate=new StartGate();
  private Image handImage;
  private Viewbox startHitLayer;
  private Button startButton;
  private BitmapSource[] handFrames;
  private bool awaitingStart,startFinished,startResuming;
  private double startWaitAt=-1,totalStartWait,lastHandStep=-1,resumeAt=-1;
  public BootWindow(Config c,string baseDir,IntPtr preferredTarget,Bounds preparedClient,Action onFirstSurfaceReady,BitmapSource preparedFirstFrame) {
   firstSurfaceReady=onFirstSurfaceReady;
   cfg=c;root=baseDir;Title=String.IsNullOrWhiteSpace(c.DisplayName)?"Dragon Codex Boot":c.DisplayName;
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=Environment.GetCommandLineArgs().Contains("--interaction-ui-test");
   AllowsTransparency=false;Topmost=true;Background=new SolidColorBrush(Color.FromRgb(250,247,253));
   target=preferredTarget!=IntPtr.Zero&&Native.IsWindow(preferredTarget)?preferredTarget:Native.FindApp(cfg);
   System.Windows.Forms.Screen screen=target!=IntPtr.Zero?System.Windows.Forms.Screen.FromHandle(target)
    :System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
   System.Drawing.Rectangle monitor=screen.WorkingArea;
   initialBounds=Geometry.FitPlayer(new Bounds(monitor.Left,monitor.Top,monitor.Width,monitor.Height),cfg.PlayerWidth,cfg.PlayerHeight);
   if(cfg.FollowClientWindow&&preparedClient.Width>=450&&preparedClient.Height>=280)initialBounds=preparedClient;
   Width=initialBounds.Width;Height=initialBounds.Height;WindowStartupLocation=WindowStartupLocation.Manual;
   Grid grid=new Grid();
   media=new MediaElement { LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,
    Stretch=Stretch.Uniform,ScrubbingEnabled=true,Volume=Geometry.Clamp(cfg.Volume) };
   grid.Children.Add(media);
   string firstFile=Path.Combine(root,"assets","startup-first-frame.png");
   BitmapSource still=preparedFirstFrame;
   if(still==null&&File.Exists(firstFile)) {
    BitmapImage first=new BitmapImage();first.BeginInit();first.CacheOption=BitmapCacheOption.OnLoad;
    first.UriSource=new Uri(firstFile);first.EndInit();first.Freeze();still=first;
   }
   if(still!=null) { openingStill=new Image { Source=still,Stretch=Stretch.Uniform,IsHitTestVisible=false };grid.Children.Add(openingStill); }
   if(cfg.InteractiveStart)BuildInteractiveStart(grid);
   status=new TextBlock { HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,
    Margin=new Thickness(20,20,20,38),FontSize=16,Foreground=new SolidColorBrush(Color.FromRgb(115,91,144)),
    Visibility=Visibility.Collapsed };
   grid.Children.Add(status);
   Button skip=new Button { Content="跳过  Esc",HorizontalAlignment=HorizontalAlignment.Right,
    VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(16),Padding=new Thickness(11,5,11,5),
    Background=Brushes.White,Foreground=new SolidColorBrush(Color.FromRgb(118,97,135)),Opacity=.45 };
   skip.Click+=delegate { SkipFromAnywhere(); };
   skip.MouseEnter+=delegate { skip.Opacity=1; };skip.MouseLeave+=delegate { skip.Opacity=.45; };
   grid.Children.Add(skip);Content=grid;
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { e.Handled=true;SkipFromAnywhere(); } };
   SourceInitialized+=delegate {
    host=new WindowInteropHelper(this).Handle;
    HwndSource.FromHwnd(host).AddHook(FixedBoundsHook);
    Native.SetWindowPos(host,new IntPtr(-1),initialBounds.Left,initialBounds.Top,initialBounds.Width,initialBounds.Height,0x10);
   };
   Loaded+=delegate {
    wall.Start();
    if(target==IntPtr.Zero)LaunchApp();
    media.Source=new Uri(Path.GetFullPath(Path.Combine(root,cfg.Video)));
    media.Pause();timer.Start();Log("started "+(cfg.FollowClientWindow?"follow-client=":"fixed-player=")+initialBounds.Width+"x"+initialBounds.Height
     +" at "+initialBounds.Left+","+initialBounds.Top);
   };
   media.MediaOpened+=delegate {
    opened=true;Bounds actual=Native.ClientBounds(host);
    Log("media-opened player-client="+actual.Width+"x"+actual.Height+" at "+actual.Left+","+actual.Top);
    Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate {
     if(closing)return;
     Native.DwmFlush(); // The opaque original first-frame surface has reached composition.
     if(firstSurfaceReady!=null)firstSurfaceReady();
     media.Play();Log("playback-started-after-first-surface");Native.SetForegroundWindow(host);
    }));
   };
   media.MediaFailed+=delegate(object sender,ExceptionRoutedEventArgs e) {
    Log("media-failed: "+e.ErrorException.Message);status.Text="视频无法播放，正在转入Codex";status.Visibility=Visibility.Visible;
    HandOff();
   };
   media.MediaEnded+=delegate { endedAt=wall.Elapsed.TotalSeconds;HandOff(); };
   timer=new DispatcherTimer(DispatcherPriority.Render);timer.Interval=TimeSpan.FromMilliseconds(16);
   timer.Tick+=Tick;
   Closed+=delegate { timer.Stop();media.Stop();DropThumbnail(); };
  }
  private void BuildInteractiveStart(Grid grid) {
   handFrames=new BitmapSource[10];
   for(int i=0;i<handFrames.Length;i++) {
    BitmapImage frame=new BitmapImage();frame.BeginInit();frame.CacheOption=BitmapCacheOption.OnLoad;
    frame.UriSource=new Uri(Path.Combine(root,"assets","start-interaction","pose-"+i.ToString("00")+".png"));
    frame.EndInit();frame.Freeze();handFrames[i]=frame;
   }
   handImage=new Image { Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed,IsHitTestVisible=false };
   grid.Children.Add(handImage);
   Canvas canvas=new Canvas { Width=1920,Height=1080 };
   startButton=new Button { Width=496,Height=128,Background=Brushes.Transparent,Cursor=Cursors.Hand,
    FocusVisualStyle=null,IsDefault=true };
   AutomationProperties.SetName(startButton,"codex start");
   ControlTemplate template=new ControlTemplate(typeof(Button));
   FrameworkElementFactory hit=new FrameworkElementFactory(typeof(Border));
   hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);hit.SetValue(Border.CornerRadiusProperty,new CornerRadius(64));
   template.VisualTree=hit;startButton.Template=template;
   Canvas.SetLeft(startButton,694);Canvas.SetTop(startButton,360);canvas.Children.Add(startButton);
   startHitLayer=new Viewbox { Stretch=Stretch.Uniform,Child=canvas,Visibility=Visibility.Collapsed };
   grid.Children.Add(startHitLayer);
   startButton.MouseEnter+=delegate { if(awaitingStart) { timer.Interval=TimeSpan.FromMilliseconds(16);startGate.Hover(true);Log("start-hover-enter"); } };
   startButton.MouseLeave+=delegate { if(awaitingStart) { timer.Interval=TimeSpan.FromMilliseconds(16);startGate.Hover(false);if(Mouse.LeftButton==MouseButtonState.Pressed)startGate.CancelPress();Log("start-hover-leave"); } };
   startButton.PreviewMouseLeftButtonDown+=delegate { if(awaitingStart) { timer.Interval=TimeSpan.FromMilliseconds(16);startGate.Press(); } };
   startButton.LostMouseCapture+=delegate { if(awaitingStart&&!startButton.IsMouseOver)startGate.CancelPress(); };
   startButton.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Space&&awaitingStart) { timer.Interval=TimeSpan.FromMilliseconds(16);startGate.Press(); } };
   startButton.Click+=delegate { if(awaitingStart&&!startGate.CommitRequested) { timer.Interval=TimeSpan.FromMilliseconds(16);startGate.Commit();Log("start-click-requested"); } };
  }
  public void SkipFromAnywhere() {
   Log("escape-or-skip phase="+(awaitingStart?"start-gate":(startResuming?"start-resume":"playback")));
   HandOff();
  }
  private bool UpdateStartGate(double videoTime) {
   if(!cfg.InteractiveStart||startFinished)return false;
   double now=wall.Elapsed.TotalSeconds;
   if(startResuming) {
    if(now-resumeAt<.1)return true;
    startFinished=true;startResuming=false;timer.Interval=TimeSpan.FromMilliseconds(16);
    totalStartWait+=now-startWaitAt;
    handImage.Visibility=Visibility.Collapsed;handImage.Source=null;
    startHitLayer.Visibility=Visibility.Collapsed;handFrames=null;
    Log("start-click-accepted playback-resumed at="+media.Position.TotalSeconds.ToString("F3"));return false;
   }
   if(!awaitingStart) {
    if(videoTime<StartGate.IdleTime)return false;
    media.Pause();media.Position=TimeSpan.FromSeconds(StartGate.IdleTime);
    awaitingStart=true;startWaitAt=now;lastHandStep=now;
    handImage.Source=handFrames[0];handImage.Visibility=Visibility.Visible;
    startHitLayer.Visibility=Visibility.Visible;startButton.Focus();
    startGate.Hover(startButton.IsMouseOver);
    Log("start-gate-paused awaiting-click at=0.500");
   }
   if(now-lastHandStep>=1.0/24) {
    lastHandStep+=1.0/24;if(now-lastHandStep>.125)lastHandStep=now;
    startGate.Step();handImage.Source=handFrames[startGate.Frame];
    if(startGate.Accepted) {
     awaitingStart=false;startResuming=true;resumeAt=now;startButton.IsEnabled=false;
     media.Position=TimeSpan.FromSeconds(StartGate.PressTime);media.Play();
     Log("start-hand-pressed resuming-original-video at=0.875");
    }
   }
   timer.Interval=TimeSpan.FromMilliseconds(awaitingStart&&!startGate.CommitRequested&&startGate.Frame==startGate.TargetFrame?100:16);
   return true;
  }
  private IntPtr FixedBoundsHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
   if(message==0x0046&&!closing&&lParam!=IntPtr.Zero) {
    Native.WindowPos pos=(Native.WindowPos)Marshal.PtrToStructure(lParam,typeof(Native.WindowPos));
    pos.X=initialBounds.Left;pos.Y=initialBounds.Top;pos.Width=initialBounds.Width;pos.Height=initialBounds.Height;
    pos.Flags&=~(uint)0x3; // Permit the fixed coordinates even for a DPI or layout size request.
    Marshal.StructureToPtr(pos,lParam,false);handled=true;
   }
   return IntPtr.Zero;
  }
  private void AlignTarget() {
   if(cfg.FollowClientWindow||!cfg.MatchClientToPlayer||target==IntPtr.Zero||alignedHandle==target)return;
   double now=wall.Elapsed.TotalSeconds;
   if(alignAttempts>=3||now-lastAlign<.2)return;
   lastAlign=now;alignAttempts++;
   bool aligned=Native.AlignClient(target,initialBounds);
   if(aligned)alignedHandle=target;
   Bounds actual=Native.ClientBounds(target);
   Log((aligned?"real-window-aligned-to-fixed-player":"real-window-alignment-inexact")
    +" desired="+initialBounds.Width+"x"+initialBounds.Height
    +" actual="+actual.Width+"x"+actual.Height+" attempt="+alignAttempts);
   Native.SetForegroundWindow(host);
  }
  private void FollowTarget(bool force) {
   double now=wall.Elapsed.TotalSeconds;
   if(!force&&now-lastFollow<.032)return;lastFollow=now;
   if(!cfg.FollowClientWindow||target==IntPtr.Zero||Native.IsIconic(target))return;
   Bounds actual=Native.ClientBounds(target);
   if(actual.Width<450||actual.Height<280)return;
   if(actual.Left==initialBounds.Left&&actual.Top==initialBounds.Top
    &&actual.Width==initialBounds.Width&&actual.Height==initialBounds.Height)return;
   Bounds previous=initialBounds;initialBounds=actual;
   if(host!=IntPtr.Zero&&!Native.SetWindowPos(host,new IntPtr(-1),actual.Left,actual.Top,actual.Width,actual.Height,0x10)) {
    initialBounds=previous;return;
   }
   if(force||now-lastFollowLog>=.5) {
    lastFollowLog=now;
    Log("player-followed-client="+actual.Width+"x"+actual.Height+" at "+actual.Left+","+actual.Top+" video=16:9-uniform");
   }
  }
  private void LaunchApp() {
   try {
    // Shell launches the packaged desktop app; its CLI helper is never used as a UI executable.
    Process.Start(new ProcessStartInfo("explorer.exe",cfg.AppLaunch) { UseShellExecute=true });
    Log("app-launch-requested");
   } catch(Exception e) { Log("app-launch-failed: "+e.Message);status.Text="无法自动打开Codex，按Esc退出";status.Visibility=Visibility.Visible; }
  }
  private void Probe() {
   double now=wall.Elapsed.TotalSeconds;
   if(probePending||now-lastProbe<.25)return;lastProbe=now;probePending=true;
   IntPtr cached=target;
   // Process enumeration and the responsiveness timeout must not block video rendering.
   ThreadPool.QueueUserWorkItem(delegate {
    IntPtr candidate=IntPtr.Zero;bool responsive=false,searched=false;
    try {
     if(cached!=IntPtr.Zero&&Native.IsWindow(cached)&&Native.IsWindowVisible(cached))candidate=cached;
     else { searched=true;candidate=Native.FindApp(cfg); }
     responsive=candidate!=IntPtr.Zero&&Native.Responsive(candidate);
    } catch {}
    try { Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {
     probePending=false;if(closing)return;
     if(searched)fullSearches++;
     if(candidate==IntPtr.Zero||!Native.IsWindow(candidate)||!responsive) { ready=false;stableHandle=IntPtr.Zero;return; }
     if(Native.IsIconic(candidate))Native.RestoreApp(candidate);
     if(candidate!=stableHandle) {
      if(thumbnail!=IntPtr.Zero)DropThumbnail();
      thumbnailFailed=false;Opacity=1;stableHandle=candidate;stableSince=wall.Elapsed.TotalSeconds;
      ready=false;alignAttempts=0;alignedHandle=IntPtr.Zero;
     } else ready=wall.Elapsed.TotalSeconds-stableSince>=.6;
     target=candidate;
     if(ready)FollowTarget(false);
    })); } catch {}
   });
  }
  private void RegisterThumbnail() {
   if(thumbnail!=IntPtr.Zero||thumbnailFailed)return;
   int hr=Native.DwmRegisterThumbnail(host,target,out thumbnail);
   if(hr!=0) { thumbnail=IntPtr.Zero;thumbnailFailed=true;Log("dwm-unavailable: "+hr); }
   else Log("live-window-connected");
  }
  private void DropThumbnail() {
   if(thumbnail!=IntPtr.Zero) { Native.DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero; }
  }
  private void Tick(object sender,EventArgs e) {
   if(closing)return;
   Probe();
   if(ready)FollowTarget(false);
   if(target!=IntPtr.Zero&&(!Native.IsWindow(target)||!Native.IsWindowVisible(target))) {
    if(targetLostAt<0)targetLostAt=wall.Elapsed.TotalSeconds;
    if(wall.Elapsed.TotalSeconds-targetLostAt>1) { Log("target-closed-or-hidden-during-animation");HandOff();return; }
   } else targetLostAt=-1;
   double interactiveWait=(awaitingStart||startResuming)?wall.Elapsed.TotalSeconds-startWaitAt:0;
   if(wall.Elapsed.TotalSeconds-totalStartWait-interactiveWait>cfg.MaxWaitSeconds+20) { Log("wait-timeout");HandOff();return; }
   if(!opened) {
    if(wall.Elapsed.TotalSeconds>12) { Log("media-open-timeout");HandOff(); }
    return;
   }
   double t=media.Position.TotalSeconds;
   if(openingStill!=null&&t>=1.0/24) { openingStill.Visibility=Visibility.Collapsed;openingStill.Source=null;openingStill=null; }
   if(UpdateStartGate(t))return;
   if(!ready&&!startedFade&&t>=cfg.HoldAt) {
    if(!holding) { media.Pause();media.Position=TimeSpan.FromSeconds(cfg.HoldAt);holding=true;Log("waiting-for-window"); }
    status.Text="Codex正在启动…  Esc跳过";status.Visibility=Visibility.Visible;
    return;
   }
   if(holding&&ready) {
    holding=false;status.Visibility=Visibility.Collapsed;media.Play();Log("window-available");
   }
   if(ready&&t>=Math.Max(0,cfg.HoldAt-.6))AlignTarget();
   if(t>=cfg.TransitionStart&&ready) {
    startedFade=true;RegisterThumbnail();
    double a=Geometry.Ease((t-cfg.TransitionStart)/(cfg.TransitionEnd-cfg.TransitionStart));
    Bounds targetBounds=Native.ClientBounds(target);
    if(targetBounds.Width<450) { HandOff();return; }
    Bounds hostBounds=initialBounds;
    if(thumbnail!=IntPtr.Zero) {
     Native.ThumbProperties props=new Native.ThumbProperties {
      Flags=1|4|8|16,Destination=Geometry.Dest(cfg.ScreenFrames,t,hostBounds.Width,hostBounds.Height,a),
      Opacity=(byte)Math.Round(255*a),Visible=true,ClientOnly=true
     };
     int hr=Native.DwmUpdateThumbnailProperties(thumbnail,ref props);
     if(hr!=0) { Log("dwm-update-failed: "+hr);DropThumbnail();thumbnailFailed=true; }
    }
    // Safe fallback gives a crossfade to the actual window if live thumbnails are unavailable.
    if(thumbnailFailed)Opacity=1-a;
   }
   if(endedAt>=0&&wall.Elapsed.TotalSeconds-endedAt>.25)HandOff();
  }
  private void HandOff() {
   if(closing)return;closing=true;
   timer.Stop();
   // Keep the live image until the covering window closes: unregistering first
   // exposes one frame of the prerecorded placeholder UI.
   // Stay with the window used by the animation, not whichever other Codex window is frontmost now.
   IntPtr app=target!=IntPtr.Zero&&Native.IsWindow(target)&&Native.IsWindowVisible(target)?target:Native.FindApp(cfg);
   if(app!=IntPtr.Zero) {
    target=app;
    if(Native.IsIconic(app))Native.RestoreApp(app);
    FollowTarget(true);
    // Removes the covering window and hands input to the real application.
    Bounds finalClient=Native.ClientBounds(app),finalPlayer=Native.ClientBounds(host);
    ClientPlacementMemory.Remember(cfg,root,app);
    Close();Native.SetForegroundWindow(app);
    Log("handed-off-to-real-window client="+finalClient.Width+"x"+finalClient.Height
     +" player="+finalPlayer.Width+"x"+finalPlayer.Height+" client-pos="+finalClient.Left+","+finalClient.Top
     +" player-pos="+finalPlayer.Left+","+finalPlayer.Top+" follow="+cfg.FollowClientWindow
     +" background-searches="+fullSearches);
   } else {
    Close();Log("closed-without-window");
   }
  }
  private void Log(string s) {
   try {
    Directory.CreateDirectory(Path.Combine(root,"logs"));
    string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+s+Environment.NewLine;
    File.AppendAllText(Path.Combine(root,"logs","launcher.log"),line);
   } catch {}
  }
 }
 public static class Entry {
  private static int Integrate(string root,string action,bool wait) {
   string script=Path.Combine(root,"scripts","integrate-entrypoints.ps1");
   if(!File.Exists(script))throw new FileNotFoundException("启动入口脚本缺失，请完整解压程序包。",script);
   string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
   // Paths are local Windows filenames (quotes cannot occur); never concatenate configuration into shell commands.
   ProcessStartInfo info=new ProcessStartInfo(powershell,"-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Action "+action+" -AppRoot \""+root.TrimEnd('\\','/')+"\"") {
    UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,WindowStyle=ProcessWindowStyle.Hidden
   };
   using(Process process=Process.Start(info)) {
    if(wait) { process.WaitForExit();return process.ExitCode; }
   }
   return 0;
  }
  public static Config ReadConfig(string root) {
   Config c=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(Path.Combine(root,"launcher.json")));
   ValidateConfig(c);
   return c;
  }
  private static bool Finite(double n) { return !Double.IsNaN(n)&&!Double.IsInfinity(n); }
  public static void ValidateConfig(Config c) {
   if(c==null||String.IsNullOrEmpty(c.Video)||String.IsNullOrEmpty(c.AppLaunch)||String.IsNullOrEmpty(c.ProcessPathContains)
    ||c.ProcessNames==null||c.ProcessNames.Length==0||c.ScreenFrames==null||c.ScreenFrames.Count<2)throw new ArgumentException("启动器配置不完整");
   string appPrefix="shell:AppsFolder\\";
   if(!c.AppLaunch.StartsWith(appPrefix,StringComparison.OrdinalIgnoreCase)
    ||!System.Text.RegularExpressions.Regex.IsMatch(c.AppLaunch.Substring(appPrefix.Length),@"^[A-Za-z0-9_.-]+![A-Za-z0-9_.-]+$"))
    throw new ArgumentException("AppLaunch必须是已安装应用的shell:AppsFolder标识。");
   if(String.IsNullOrWhiteSpace(c.Video)||String.IsNullOrWhiteSpace(c.ProcessPathContains)
    ||c.ProcessNames.Any(n=>String.IsNullOrWhiteSpace(n)||!System.Text.RegularExpressions.Regex.IsMatch(n,@"^[A-Za-z0-9_.-]+$")))
    throw new ArgumentException("视频路径或进程匹配配置无效。");
   if(!Finite(c.HoldAt)||!Finite(c.TransitionStart)||!Finite(c.TransitionEnd)||!Finite(c.MaxWaitSeconds)||!Finite(c.Volume)
    ||c.Volume<0||c.Volume>1)throw new ArgumentException("时间和音量必须是有限数值，音量范围为0至1。");
   if(c.InteractiveStart&&c.HoldAt<=StartGate.PressTime)throw new ArgumentException("交互启动节点必须早于窗口等待节点。");
   if(c.HoldAt<=0||c.TransitionStart<=c.HoldAt||c.TransitionEnd<=c.TransitionStart||c.MaxWaitSeconds<10)throw new Exception("启动时间配置不正确");
   if(c.PlayerWidth<450||c.PlayerHeight<280||c.PlayerWidth>7680||c.PlayerHeight>4320)throw new Exception("播放窗口尺寸配置不正确");
   for(int i=0;i<c.ScreenFrames.Count;i++) {
    ScreenFrame f=c.ScreenFrames[i];
    if(f==null||!Finite(f.Time)||!Finite(f.X)||!Finite(f.Y)||!Finite(f.Width)||!Finite(f.Height)||f.Time<0)
     throw new ArgumentException("巨幕关键帧必须使用非负时间和有限坐标。");
    if(f.Width<=0||f.Height<=0||f.X<0||f.Y<0||f.X+f.Width>1.001||f.Y+f.Height>1.001
     ||(i>0&&f.Time<=c.ScreenFrames[i-1].Time))throw new Exception("巨幕位置配置不正确");
   }
  }
  private static void Assert(bool b,string why) { if(!b)throw new Exception("Self-test: "+why); }
  private static int SelfTest(string root) {
   Config c=ReadConfig(root);
   if(c.StartupCurtain) {
    string firstFile=Path.Combine(root,"assets","startup-first-frame.png");Assert(File.Exists(firstFile),"startup first frame missing");
    using(System.Drawing.Image first=System.Drawing.Image.FromFile(firstFile))Assert(first.Width==1920&&first.Height==1080,"startup first frame is native 16:9");
   }
   int interactionFrameCount=0;
   if(c.InteractiveStart)for(int i=0;i<10;i++) {
    string file=Path.Combine(root,"assets","start-interaction","pose-"+i.ToString("00")+".png");
    Assert(File.Exists(file),"interaction frame missing: "+i);
    using(System.Drawing.Image frame=System.Drawing.Image.FromFile(file))Assert(frame.Width==1920&&frame.Height==1080,"interaction frame keeps native 16:9");
    interactionFrameCount++;
   }
   Assert(Geometry.Ease(0)==0&&Geometry.Ease(1)==1,"fade endpoints");
   int cases=0;
   foreach(int[] size in new int[][] {new int[]{1920,1080},new int[]{1536,864},new int[]{1200,900},new int[]{2560,1440},new int[]{900,1400}}) {
    for(int n=0;n<=120;n++) {
     double t=c.TransitionStart+(c.TransitionEnd-c.TransitionStart)*n/120.0,a=Geometry.Ease(n/120.0);
     Bounds b=Geometry.Dest(c.ScreenFrames,t,size[0],size[1],a);
     Assert(b.Width>0&&b.Height>0&&b.Left>=0&&b.Top>=0&&b.Right<=size[0]+1&&b.Bottom<=size[1]+1,"viewport "+n);cases++;
    }
    Bounds last=Geometry.Dest(c.ScreenFrames,c.TransitionEnd,size[0],size[1],1);
    Assert(last.Left==0&&last.Top==0&&last.Right==size[0]&&last.Bottom==size[1],"pixel-aligned final frame");
   }
   Bounds negativeMonitor=new Bounds(-1920,0,1920,1080),target=new Bounds(-1500,100,1200,800);
   Bounds end=Geometry.Lerp(negativeMonitor,target,1);Assert(end.Left==target.Left&&end.Right==target.Right,"negative monitor");
   int placementCases=0;
   foreach(Bounds monitor in new Bounds[] {new Bounds(0,0,1920,1080),new Bounds(0,0,2560,1440),
    new Bounds(0,0,3840,2160),new Bounds(-2560,0,2560,1440),new Bounds(1920,-1440,2560,1440),new Bounds(0,0,1366,768)}) {
    Bounds fixedPlayer=Geometry.Centered(monitor,c.PlayerWidth,c.PlayerHeight);
    Assert(fixedPlayer.Width==c.PlayerWidth&&fixedPlayer.Height==c.PlayerHeight,"fixed physical video size");
    Assert(Math.Abs(fixedPlayer.Left*2+fixedPlayer.Width-monitor.Left*2-monitor.Width)<=1
     &&Math.Abs(fixedPlayer.Top*2+fixedPlayer.Height-monitor.Top*2-monitor.Height)<=1,"centered monitor");placementCases++;
    Bounds oldWindow=new Bounds(monitor.Left+10,monitor.Top+20,1216,816),oldClient=new Bounds(monitor.Left+18,monitor.Top+28,1200,800);
    Bounds aligned=Geometry.WindowForClient(oldWindow,oldClient,fixedPlayer);
    Assert(aligned.Left+8==fixedPlayer.Left&&aligned.Top+8==fixedPlayer.Top
     &&aligned.Width-16==fixedPlayer.Width&&aligned.Height-16==fixedPlayer.Height,"client frame insets");placementCases++;
   }
   Directory.CreateDirectory(Path.Combine(root,"qa"));
   File.WriteAllText(Path.Combine(root,"qa","self-test.json"),new JavaScriptSerializer().Serialize(new {
    ok=true,geometryCases=cases,fixedPlacementCases=placementCases,playerWidth=c.PlayerWidth,playerHeight=c.PlayerHeight,
    interactiveStart=c.InteractiveStart,interactionFrameCount=interactionFrameCount,startupCurtain=c.StartupCurtain,
    videoExists=File.Exists(Path.GetFullPath(Path.Combine(root,c.Video))),nativeUiTested=false,
    scope="Configuration and geometry only. No desktop window or media is opened."
   }));
   return 0;
  }
  private static void StartupLog(string root,string line) {
   try { Directory.CreateDirectory(Path.Combine(root,"logs"));
    File.AppendAllText(Path.Combine(root,"logs","launcher.log"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+line+Environment.NewLine);
   } catch {}
  }
  private static bool StartPlayback(Application app,Config c,string root,IntPtr target,StartupCurtains curtains) {
   // Recheck immediately before constructing the covering window. Never fall back to
   // a fixed-size visible animation while an existing background app is reopening.
   if(target==IntPtr.Zero||!Native.IsWindow(target)||!Native.IsWindowVisible(target)||Native.IsIconic(target))return false;
   Bounds client=Native.ClientBounds(target);if(client.Width<450||client.Height<280)return false;
   if(curtains!=null)curtains.Apply(client,"confirmed-client");
   ClientPlacementMemory.Remember(c,root,target);
   app.ShutdownMode=ShutdownMode.OnMainWindowClose;
   BootWindow window=new BootWindow(c,root,target,client,delegate {
    if(curtains!=null) { curtains.Dispose();StartupLog(root,"startup-curtains-released after-opaque-player-first-surface"); }
   },curtains==null?null:curtains.FirstFrame);app.MainWindow=window;window.Show();return true;
  }
  private static void StartWithRestore(Application app,Config c,string root,StartupCurtains curtains) {
   WindowReadyGate gate=new WindowReadyGate();Stopwatch elapsed=Stopwatch.StartNew();
   DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Normal);timer.Interval=TimeSpan.FromMilliseconds(50);
   bool pending=false,finished=false,launchRequested=false;IntPtr restoreRequested=IntPtr.Zero;
   double lastProbe=-1;IntPtr lastCandidate=IntPtr.Zero;
   StartupLog(root,"window-preflight-start waiting-for-visible-stable-client");
   app.Exit+=delegate { finished=true;timer.Stop(); };
   timer.Tick+=delegate {
    if(finished||pending||elapsed.Elapsed.TotalSeconds-lastProbe<.2)return;
    if(elapsed.Elapsed.TotalSeconds>c.MaxWaitSeconds) {
     finished=true;timer.Stop();StartupLog(root,"window-preflight-timeout animation-cancelled-no-fallback-resize");app.Shutdown();return;
    }
    pending=true;lastProbe=elapsed.Elapsed.TotalSeconds;IntPtr cached=lastCandidate;
    ThreadPool.QueueUserWorkItem(delegate {
     IntPtr candidate=IntPtr.Zero;Bounds actual=new Bounds();bool usable=false,minimized=false;
     try {
      candidate=cached!=IntPtr.Zero&&Native.IsWindow(cached)&&Native.IsWindowVisible(cached)?cached:Native.FindApp(c);
      if(candidate!=IntPtr.Zero) {
       minimized=Native.IsIconic(candidate);
       actual=Native.ClientBounds(candidate);
       usable=!minimized&&Native.IsWindowVisible(candidate)&&Native.Responsive(candidate);
      }
     } catch {}
     try { app.Dispatcher.BeginInvoke(DispatcherPriority.Normal,new Action(delegate {
      pending=false;if(finished||app.Dispatcher.HasShutdownStarted)return;
      double now=elapsed.Elapsed.TotalSeconds;
      if(candidate==IntPtr.Zero&&!launchRequested) {
       launchRequested=true;
       try { Process.Start(new ProcessStartInfo("explorer.exe",c.AppLaunch) { UseShellExecute=true });
        StartupLog(root,"client-open-requested background-or-closed-window");
       } catch(Exception e) { finished=true;timer.Stop();StartupLog(root,"client-open-failed: "+e.Message);app.Shutdown(1);return; }
      }
      if(curtains!=null&&candidate!=IntPtr.Zero)curtains.MatchWindow(candidate);
      if(minimized&&candidate!=restoreRequested) {
       restoreRequested=candidate;Native.RestoreApp(candidate);
       StartupLog(root,"minimized-client-detected restoring-before-playback");
      }
      if(candidate!=lastCandidate) {
       lastCandidate=candidate;
       StartupLog(root,"window-preflight-candidate changed="+(candidate!=IntPtr.Zero));
      }
      if(gate.Observe(candidate,actual,usable,now)&&StartPlayback(app,c,root,candidate,curtains)) {
       finished=true;timer.Stop();
       StartupLog(root,"window-preflight-ready client="+actual.Width+"x"+actual.Height
        +" at "+actual.Left+","+actual.Top+" maximized="+Native.IsZoomed(candidate)
        +" reopened="+launchRequested);
      }
     })); } catch {}
    });
   };
   timer.Start();
  }
  [STAThread]
  public static int Main(string[] args) {
   string root=AppDomain.CurrentDomain.BaseDirectory;
   try {
    // Headless checks exit before any desktop API or media window is constructed.
    if(args.Contains("--self-test"))return SelfTest(root);
    if(args.Contains("--remember-client-window")) {Config remember=ReadConfig(root);IntPtr known=Native.FindApp(remember);if(known==IntPtr.Zero)known=Native.FindApp(remember,true);ClientPlacementMemory.Remember(remember,root,known);return 0;}
    if(args.Contains("--restore-entrypoints"))return Integrate(root,"Restore",true);
    if(args.Contains("--integrate-entrypoints"))return Integrate(root,"Install",true);
    bool owns;using(Mutex mutex=new Mutex(true,"Local\\DragonCodexBoot",out owns)) {
     if(!owns)return 0;
     Config c=ReadConfig(root);
     if(!File.Exists(Path.GetFullPath(Path.Combine(root,c.Video))))throw new Exception("未找到启动视频。请先运行scripts/Import-Animation.ps1导入自己的MP4，或修改launcher.json的Video路径。");
     if(c.AutoReplaceEntrypoints) {
      try { Integrate(root,"Auto",false); }
      catch(Exception e) {
       try { Directory.CreateDirectory(Path.Combine(root,"logs"));File.AppendAllText(Path.Combine(root,"logs","launcher.log"),"entrypoint-integration: "+e.Message+Environment.NewLine); } catch {}
      }
     }
     Application app=new Application();
     app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) {
      e.Handled=true;MessageBox.Show("启动器已退出："+e.Exception.Message,"Codex启动");app.Shutdown(1);
     };
     const int escapeId=0x4511;
     bool escapeRegistered=Native.RegisterHotKey(IntPtr.Zero,escapeId,0x4000,0x1B);
     ThreadMessageEventHandler escapeHandler=delegate(ref MSG message,ref bool handled) {
      if(message.message==0x0312&&message.wParam.ToInt32()==escapeId) {
       handled=true;BootWindow active=app.MainWindow as BootWindow;
       if(active!=null)active.SkipFromAnywhere();else { StartupLog(root,"escape-during-window-preflight animation-cancelled");app.Shutdown(); }
      }
     };
     if(escapeRegistered)ComponentDispatcher.ThreadFilterMessage+=escapeHandler;
     else StartupLog(root,"escape-hotkey-unavailable window-preview-key-remains-enabled");
     app.Exit+=delegate { if(escapeRegistered) { ComponentDispatcher.ThreadFilterMessage-=escapeHandler;Native.UnregisterHotKey(IntPtr.Zero,escapeId); } };
     app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
     app.Startup+=delegate {
      try { using(Process self=Process.GetCurrentProcess())self.PriorityClass=ProcessPriorityClass.BelowNormal; }
      catch(Exception e) { StartupLog(root,"launcher-priority-unchanged: "+e.Message); }
      StartupCurtains curtains=c.StartupCurtain?new StartupCurtains(c,root):null;
      app.Exit+=delegate { if(curtains!=null)curtains.Dispose(); };
      if(curtains!=null)curtains.Show();
      app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate {
       if(app.Dispatcher.HasShutdownStarted)return;
       if(curtains!=null) { Native.DwmFlush();StartupLog(root,"startup-curtains-presented count="+curtains.Count+" before-client-open-or-restore"); }
       StartWithRestore(app,c,root,curtains);
      }));
     };
     return app.Run();
    }
   } catch(Exception e) {
    if(args.Contains("--self-test")) { File.WriteAllText(Path.Combine(root,"self-test-error.txt"),e.ToString());return 1; }
    MessageBox.Show(e.Message,"Codex启动");return 1;
   }
  }
 }
}
