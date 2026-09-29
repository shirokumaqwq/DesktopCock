using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopCock.Core;

namespace DesktopCock;
internal sealed class PreviewWindow : Window
{
    private readonly PetWindow pet;
    private readonly TextBlock status;
    private readonly CheckBox night, idle, flightDebug;
    private readonly ComboBox skinPicker;
    private readonly Image birdImage = new() { Width=144,Height=144,Stretch=Stretch.Fill,RenderTransformOrigin=new Point(.5,.5),
        HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false };
    private readonly TextBlock songNote = new() { Text="♪",FontSize=22,Foreground=Brushes.DarkGoldenrod,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(125,12,0,0) };
    private readonly Dictionary<Mood,string> names = new()
    {
        [Mood.Idle]="侧面 / 正面 / 眨眼", [Mood.Walk]="交替迈步 · 12 帧", [Mood.Beg]="低头求摸 / 冠羽轻动",
        [Mood.Pet]="享受摸头", [Mood.Sing]="叽咕叽 ♪", [Mood.Sleep]="睡觉 / 呼吸",
        [Mood.Stretch]="醒来伸翅", [Mood.Look]="好奇转头", [Mood.Lift]="抬起爪子", [Mood.Bite]="生气啄咬"
    };
    internal PreviewWindow(PetWindow pet)
    {
        this.pet = pet;
        Title = "玄凤的观察室"; Width = 480; Height = 760; MinWidth = 420; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(247,245,238));
        FontFamily = new FontFamily("Microsoft YaHei UI"); Foreground = new SolidColorBrush(Color.FromRgb(44,55,48));
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = "DESKTOP COCKATIEL  /  01", FontSize = 11, Foreground = Brushes.DarkOliveGreen });
        content.Children.Add(new TextBlock { Text = "玄凤的观察室", FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,12,0,6) });
        var stage = new Grid { Height=232,Margin=new Thickness(0,4,8,12),Background=new SolidColorBrush(Color.FromRgb(237,234,219)) };
        RenderOptions.SetBitmapScalingMode(birdImage,BitmapScalingMode.NearestNeighbor);
        stage.Children.Add(new Border { Height=2,Background=new SolidColorBrush(Color.FromRgb(183,174,149)),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(34,0,34,19) });
        stage.Children.Add(birdImage); stage.Children.Add(songNote); content.Children.Add(stage);
        content.Children.Add(new TextBlock { Text = "羽色", FontSize = 13, Margin = new Thickness(0,0,0,6) });
        skinPicker = new ComboBox
        {
            ItemsSource = pet.Skins, DisplayMemberPath = nameof(SkinOption.Name), SelectedValuePath = nameof(SkinOption.Id),
            SelectedValue = pet.CurrentSkin, Margin = new Thickness(0,0,8,14), Padding = new Thickness(8)
        };
        skinPicker.SelectionChanged += (_,_) =>
        {
            if (skinPicker.SelectedValue is string id && id != pet.CurrentSkin) pet.SetSkin(id);
        };
        content.Children.Add(skinPicker);
        flightDebug=new CheckBox { Content="飞行 Debug · Alt＋点击绿色区间试飞",Margin=new Thickness(0,0,0,10),IsChecked=pet.FlightDebugEnabled };
        flightDebug.Click+=(_,_)=> { pet.SetFlightDebug(flightDebug.IsChecked==true);Refresh(); };
        content.Children.Add(flightDebug);
        var interval=new Button { Content="设置探索间隔…",Margin=new Thickness(0,0,8,12),Padding=new Thickness(8) };
        interval.Click+=(_,_)=>pet.ShowExploreSettings();content.Children.Add(interval);
        content.Children.Add(new TextBlock { Text = "点选姿势预览，或恢复自然活动观察它的反应。\n摸头：在鸟头上按住左键，缓慢来回移动。", FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,20) });
        var buttons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var (mood,label) in names)
        {
            var button = new Button { Content = label, Margin = new Thickness(0,0,8,8), Padding = new Thickness(10), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(216,220,208)) };
            button.Click += (_,_) => { pet.SetPaused(false); pet.PreviewAction(mood); Refresh(); };
            buttons.Children.Add(button);
        }
        content.Children.Add(buttons);
        var gazeButtons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var (gaze, label) in new[] { (Gaze.UpDiagonal, "斜向上抬头"), (Gaze.UpFront, "正面向上抬头") })
        {
            var button = new Button { Content = label, Margin = new Thickness(0,0,8,8), Padding = new Thickness(10) };
            button.Click += (_,_) => { pet.SetPaused(false); pet.PreviewAction(Mood.Look,gaze); Refresh(); };
            gazeButtons.Children.Add(button);
        }
        content.Children.Add(gazeButtons);
        var turnButtons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var lookBack = new Button { Content = "回头 · 身体不动", Margin = new Thickness(0,0,8,8), Padding = new Thickness(10) };
        lookBack.Click += (_,_) => { pet.SetPaused(false); pet.PreviewAction(Mood.Look,head: HeadYaw.Back); Refresh(); };
        var turn = new Button { Content = "踏步转身", Margin = new Thickness(0,0,8,8), Padding = new Thickness(10) };
        turn.Click += (_,_) => { pet.SetPaused(false); pet.PreviewAction(Mood.Look, turn: true); Refresh(); };
        turnButtons.Children.Add(lookBack); turnButtons.Children.Add(turn); content.Children.Add(turnButtons);
        var resume = new Button { Content = "恢复自然活动", Padding = new Thickness(12), Margin = new Thickness(0,4,8,16), Background = new SolidColorBrush(Color.FromRgb(224,232,205)), BorderThickness = new Thickness(0) };
        resume.Click += (_,_) => { pet.SetPaused(false); pet.ResumeNaturalActivity(); Refresh(); };
        content.Children.Add(resume);
        night = new CheckBox { Content = "模拟夜间（23:00）", Margin = new Thickness(0,0,0,10) };
        idle = new CheckBox { Content = "模拟电脑长时间空闲", Margin = new Thickness(0,0,0,14) };
        night.Click += (_,_) => { pet.SimulateNight = night.IsChecked == true; pet.ResumeNaturalActivity(); };
        idle.Click += (_,_) => { pet.SimulateIdle = idle.IsChecked == true; pet.ResumeNaturalActivity(); };
        content.Children.Add(night); content.Children.Add(idle);
        status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
        content.Children.Add(status);
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    internal void Refresh()
    {
        var next = $"{names[pet.DisplayedGround.State]}  ·  烦躁 {pet.Bird.Annoyance:0}/100{(pet.IsPaused ? "  ·  已暂停" : "")}\n{pet.EnvironmentStatus}\n关闭观察室后自动恢复自然活动。";
        if (status.Text != next) status.Text = next;
        if(pet.FlightDebugEnabled) status.Text+="\n"+pet.FlightStatus;
        flightDebug.IsChecked=pet.FlightDebugEnabled;
        idle.IsChecked = pet.SimulateIdle;
        night.IsChecked = pet.SimulateNight;
        if (skinPicker.SelectedValue as string != pet.CurrentSkin) skinPicker.SelectedValue = pet.CurrentSkin;
    }
    internal void ShowSprite(Sprite sprite,int direction,bool singing)
    {
        const double scale=2.25;
        birdImage.Width=sprite.Width*scale;birdImage.Height=sprite.Height*scale;
        double footX=direction==1?sprite.Spec.Foot[0]:sprite.Width-sprite.Spec.Foot[0];
        // Keep the feet on the stage's line when canvas size or pose changes.
        double centerOffset=(sprite.Width/2.0-footX)*scale;
        birdImage.Margin=new Thickness(centerOffset,211-sprite.Spec.Foot[1]*scale,-centerOffset,0);
        if(!ReferenceEquals(birdImage.Source,sprite.Bitmap)) birdImage.Source=sprite.Bitmap;
        if(birdImage.RenderTransform is not ScaleTransform t || t.ScaleX != direction) birdImage.RenderTransform=new ScaleTransform(direction,1);
        songNote.Visibility=singing ? Visibility.Visible : Visibility.Collapsed;
    }
}
