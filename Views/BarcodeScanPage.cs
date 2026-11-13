#if ANDROID
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace SiteSyncTracker.Views;

public sealed class BarcodeScanPage:ContentPage
{
    private readonly TaskCompletionSource<string> _result=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CameraBarcodeReaderView _camera;
    public Task<string> Result=>_result.Task;
    public BarcodeScanPage()
    {
        Title="Scan QR / barcode";
        _camera=new CameraBarcodeReaderView {Options=new BarcodeReaderOptions {Formats=BarcodeFormats.All,AutoRotate=true,Multiple=false}};
        _camera.BarcodesDetected+=async (_,args)=>
        {
            var value=args.Results.FirstOrDefault()?.Value;
            if(string.IsNullOrWhiteSpace(value)||_result.Task.IsCompleted)return;
            _camera.IsDetecting=false;
            _result.TrySetResult(value);
            await MainThread.InvokeOnMainThreadAsync(async()=>await Navigation.PopModalAsync());
        };
        Content=_camera;
    }
    protected override void OnDisappearing()
    {
        _camera.IsDetecting=false;_camera.Handler?.DisconnectHandler();
        _result.TrySetException(new OperationCanceledException("Scanning cancelled."));
        base.OnDisappearing();
    }
}
#endif
