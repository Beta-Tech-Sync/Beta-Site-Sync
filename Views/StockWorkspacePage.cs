using System.Text.Json;
using SiteSyncTracker.Services;
using SiteSyncTracker.Workspace;
#if ANDROID
using Android.Nfc;
#endif

namespace SiteSyncTracker.Views;

public sealed class StockWorkspacePage : ContentPage
{
    private readonly HybridWebView _web;
    private readonly WorkspaceStore _store;
#if ANDROID
    private NfcAdapter? _nfc;
    private NfcReader? _reader;
#endif
    public StockWorkspacePage()
    {
        Title="Stock & deliveries";
        var service=IPlatformApplication.Current!.Services.GetRequiredService<InventoryService>();
        _store=new WorkspaceStore(service);
        _web=new HybridWebView {HybridRoot="InventoryUi",DefaultFile="maui.html"};
        _web.RawMessageReceived+=OnRequest;
        Content=_web;
    }
    private async void OnRequest(object? sender,HybridWebViewRawMessageReceivedEventArgs args)
    {
        string? id=null;
        try
        {
            using var json=JsonDocument.Parse(args.Message);
            var root=json.RootElement;id=root.GetProperty("id").GetString();
            var action=root.GetProperty("action").GetString();
            object? result=action switch
            {
                "load"=>await _store.LoadAsync(),
                "save"=>await _store.SaveAsync(root.GetProperty("payload").Deserialize<WorkspaceDocument>(WorkspaceRules.Json)!),
                "location"=>await LocationAsync(),
                "import"=>await ImportAsync(),
                "export"=>await ExportAsync(root.GetProperty("payload")),
                "scan"=>await ScanAsync(),
                "nfc"=>await NfcAsync(),
                _=>throw new ArgumentException("Unsupported device action.")
            };
            await ReplyAsync(new {id,value=result});
        }
        catch(Exception e) {await ReplyAsync(new {id,error=e.Message});}
    }
    private Task ReplyAsync(object response)=>MainThread.InvokeOnMainThreadAsync(async ()=>
        await _web.EvaluateJavaScriptAsync("window.stockNativeResult("+JsonSerializer.Serialize(response,WorkspaceRules.Json)+");"));
    private static async Task<Coordinates> LocationAsync()
    {
        var location=await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High,TimeSpan.FromSeconds(20)))
            ??throw new InvalidOperationException("Location unavailable; enter the site manually.");
        return new Coordinates {Latitude=location.Latitude,Longitude=location.Longitude,Accuracy=location.Accuracy??0};
    }
    private static async Task<string?> ImportAsync()
    {
        var file=await FilePicker.Default.PickAsync(new PickOptions {PickerTitle="Choose SiteSync CSV or JSON"});
        if(file is null)return null;
        await using var stream=await file.OpenReadAsync();
        using var memory=new MemoryStream();
        var buffer=new byte[8192];int read;
        while((read=await stream.ReadAsync(buffer))>0){if(memory.Length+read>20_000_000)throw new ArgumentException("File exceeds 20 MB.");await memory.WriteAsync(buffer.AsMemory(0,read));}
        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }
    private static async Task<bool> ExportAsync(JsonElement request)
    {
        var name=Path.GetFileName(request.GetProperty("name").GetString()!);
        if(!name.EndsWith(".json")&&!name.EndsWith(".csv")&&!name.EndsWith(".html"))throw new ArgumentException("Unsupported export type.");
        var contents=request.GetProperty("contents").GetString()!;
        if(contents.Length>20_000_000)throw new ArgumentException("Export exceeds 20 MB.");
        var directory=Path.Combine(FileSystem.CacheDirectory,"stock-exports");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,name);await File.WriteAllTextAsync(path,contents);
        await Share.Default.RequestAsync(new ShareFileRequest {Title="Save or share SiteSync file",File=new ShareFile(path,request.GetProperty("type").GetString())});
        return true;
    }
    private async Task<string> ScanAsync()
    {
#if ANDROID
        if(await Permissions.RequestAsync<Permissions.Camera>()!=PermissionStatus.Granted)throw new InvalidOperationException("Camera permission was not granted. Enter the code instead.");
        var page=new BarcodeScanPage();
        await Navigation.PushModalAsync(new NavigationPage(page));
        return await page.Result;
#else
        return await DisplayPromptAsync("Scan item","Use your USB/Bluetooth reader or enter the QR/barcode value.","Use code","Cancel")
            ??throw new OperationCanceledException("Scan cancelled.");
#endif
    }
    private async Task<string> NfcAsync()
    {
#if ANDROID
        _nfc=NfcAdapter.GetDefaultAdapter(Android.App.Application.Context);
        if(_nfc is null||!_nfc.IsEnabled)throw new InvalidOperationException("NFC is unavailable or switched off on this device.");
        var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _reader=new NfcReader(completion);
        _nfc.EnableReaderMode(Platform.CurrentActivity!,_reader,
            NfcReaderFlags.NfcA|NfcReaderFlags.NfcB|NfcReaderFlags.NfcF|NfcReaderFlags.NfcV|NfcReaderFlags.SkipNdefCheck,null);
        try{return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));}
        finally{_nfc.DisableReaderMode(Platform.CurrentActivity!);_reader.Dispose();_reader=null;}
#else
        throw new InvalidOperationException("Use an external NFC/RFID reader in keyboard mode on Windows.");
#endif
    }
    protected override void OnDisappearing()
    {
#if ANDROID
        if(_nfc is not null&&_reader is not null)_nfc.DisableReaderMode(Platform.CurrentActivity!);
#endif
        base.OnDisappearing();
    }
#if ANDROID
    private sealed class NfcReader(TaskCompletionSource<string> completion):Java.Lang.Object,NfcAdapter.IReaderCallback
    {
        public void OnTagDiscovered(Tag? tag)
        {
            var id=tag?.GetId();if(id is {Length:>0})completion.TrySetResult(Convert.ToHexString(id));
        }
    }
#endif
}
