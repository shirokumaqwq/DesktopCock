using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopCock.Core;

namespace DesktopCock;

internal sealed class LearningWindow : Window
{
    private readonly PetWindow pet;
    private readonly Button teach = new() { Content="按住教它说话",Height=44,Margin=new Thickness(0,12,0,8) };
    private readonly CheckBox automatic = new() { Content="开启自动学习（仅在此窗口打开期间）",Margin=new Thickness(0,0,0,12) };
    private readonly TextBlock status = new() { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8),MinHeight=64 };
    private readonly ListBox phrases = new() { MinHeight=120 };
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(250) };
    private bool holding, refreshing;
    private string listKey="";
    internal LearningWindow(PetWindow owner)
    {
        pet=owner; Title="教玄凤说话";Width=600;Height=570;MinWidth=520;MinHeight=510;
        FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=14;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var panel=new DockPanel { Margin=new Thickness(20) }; Content=panel;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);
        top.Children.Add(new TextBlock { Text="只在你按住按钮或开启自动学习时使用麦克风。录音与记忆保存在本机。",TextWrapping=TextWrapping.Wrap });
        top.Children.Add(teach);top.Children.Add(automatic);
        top.Children.Add(new TextBlock { Text="当前支持记住短语与熟练度。真实玄凤学舌音色仍在验证；导入你认可的玄凤示范后，该短语才会发声。",TextWrapping=TextWrapping.Wrap });
        top.Children.Add(status);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0) };
        DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        var remove=new Button { Content="删除选中记录与录音",Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,10,0) };
        var import=new Button { Content="导入玄凤示范…",Padding=new Thickness(10,6,10,6) };
        actions.Children.Add(remove);actions.Children.Add(import);panel.Children.Add(phrases);
        remove.Click+=(_,_) => Execute(() => { if (phrases.SelectedItem is ListBoxItem { Tag: LearnedPhrase p }) pet.RemoveLearnedPhrase(p.Id); });
        import.Click+=(_,_) => Execute(() =>
        {
            if (phrases.SelectedItem is not ListBoxItem { Tag: LearnedPhrase p }) return;
            var dialog=new Microsoft.Win32.OpenFileDialog { Filter="已认可的玄凤学舌录音 (*.wav)|*.wav",Title="选择这句短语的玄凤示范（16位单声道 WAV）" };
            if (dialog.ShowDialog(this)==true) pet.Learning.ImportVoice(p.Id,dialog.FileName);
        });
        teach.PreviewMouseLeftButtonDown+=(_,e)=> { Begin();if(holding)teach.CaptureMouse();e.Handled=true; };
        teach.PreviewMouseLeftButtonUp+=(_,e)=> { End(true);e.Handled=true; };
        teach.LostMouseCapture+=(_,_)=>End(false);
        teach.PreviewKeyDown+=(_,e)=> { if(e.Key==Key.Space&&!e.IsRepeat){Begin();e.Handled=true;} };
        teach.PreviewKeyUp+=(_,e)=> { if(e.Key==Key.Space){End(true);e.Handled=true;} };
        Deactivated+=(_,_)=>End(false);
        automatic.Checked+=(_,_)=> { if(!refreshing)pet.SetAutomaticLearning(true); };
        automatic.Unchecked+=(_,_)=> { if(!refreshing)pet.SetAutomaticLearning(false); };
        timer.Tick+=(_,_)=>Refresh();timer.Start();
        Closed+=(_,_)=> { End(false);timer.Stop(); };
        Refresh();
    }
    private void Begin() { if(!holding)holding=pet.BeginTeaching();Refresh(); }
    private void End(bool submit)
    { if(!holding)return;holding=false;pet.EndTeaching(submit);teach.ReleaseMouseCapture();Refresh(); }
    private void Execute(Action action)
    {
        try { action();listKey="";Refresh(); }
        catch(Exception e) when(AudioService.IsAudioFailure(e)) { MessageBox.Show(this,e.Message,"操作未完成"); }
    }
    private void Refresh()
    {
        refreshing=true;
        try
        {
            teach.IsEnabled=pet.Learning.Available&&(!pet.Learning.Busy||pet.IsTeaching);
            teach.Content=pet.IsTeaching ? "正在听…松开结束" : "按住教它说话";
            automatic.IsEnabled=pet.Learning.Available&&!pet.IsTeaching;automatic.IsChecked=pet.AutomaticLearning;
            status.Text=pet.Learning.Available ? pet.LearningCaptureStatus+"\n"+pet.Learning.Status :
                "尚未安装本地语音组件。请按使用说明完成一次安装，之后可离线教学。当前麦克风未开启。";
            var entries=pet.Learning.Phrases;
            var key=string.Join("|",entries.Select(p=>$"{p.Id}:{p.Repetitions}"));
            if(key!=listKey)
            {
                var selected=(phrases.SelectedItem as ListBoxItem)?.Tag as LearnedPhrase;
                phrases.Items.Clear();
                foreach(var entry in entries)
                {
                    var familiarity=entry.Familiarity*Math.Pow(.5,Math.Max(0,(DateTimeOffset.UtcNow-entry.LastHeard).TotalDays)/30);
                    var item=new ListBoxItem { Tag=entry,Content=$"{entry.Text}　·　{entry.Repetitions} 次 / 熟悉度 {familiarity:0.0}　·　"+
                        (pet.Learning.HasVoice(entry.Id) ? "已有玄凤示范" : "等待玄凤音色") };
                    phrases.Items.Add(item);if(entry.Id==selected?.Id)phrases.SelectedItem=item;
                }
                listKey=key;
            }
        }
        finally { refreshing=false; }
    }
}
