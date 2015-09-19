using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

//追加
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using CoreTweet;



// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace SecurityWebcam
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture capture;
        private StorageFile recordFile;
        private bool isRecording;
        private const int inputPin = 5;
        private GpioPin pin;
        private DispatcherTimer timer;
        private const string fileName = "video.mp4";
        private const string cKey = "[your consumerKey]";
        private const string cSecret = "[your consumerSecret]";
        private const string aToken = "[your access Token]";
        private const string aSecret = "[your access Secret]";


        public MainPage()
        {
            InitializeComponent();

            isRecording = false;

            InitVideo();
            InitGpio();
        }

        private void InitTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(30);
            timer.Tick += Timer_Tick;
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitGpio()
        {
            var gpio = GpioController.GetDefault();
            if (gpio== null)
            {
                Debug.WriteLine("Can't find GpioController.");
                return;
            }

            pin = gpio.OpenPin(inputPin);

            if (pin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
            {
                pin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            }
            else
            {
                pin.SetDriveMode(GpioPinDriveMode.Input);
            }

            Debug.WriteLine("GPIO initializing...");

            //Sleep
            for (int i = 0; i <= 10000; i++) { }

            //Event
            pin.ValueChanged += Pin_ValueChanged;

            Debug.WriteLine("GPIO initialized.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (args.Edge == GpioPinEdge.RisingEdge)
            {
                if (isRecording == false)
                {
                    RecordingStart();
                }
            }
            if (args.Edge == GpioPinEdge.FallingEdge)
            {
                if (isRecording == true)
                {
                    RecordingStop();
                    Upload();
                    InitVideo();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        private async void SendDm(string fileName)
        {
            var tokens = Tokens.Create(cKey, cSecret, aToken, aSecret);
            string msg = "Upload Video." + "https://securitywebcam.blob.core.windows.net/video/" + fileName;
            await tokens.DirectMessages.NewAsync(new { screen_name = "linyixian", text = msg });
        }


        /// <summary>
        /// 
        /// </summary>
        private async void InitVideo()
        {
            try
            {
                if (capture != null)
                {
                    if (isRecording)
                    {
                        await capture.StopRecordAsync();
                        isRecording = false;
                    }

                    capture.Dispose();
                    capture = null;
                }

                //Search Capture Device
                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.VideoDeviceId = "";
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo;

                var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                if (devices.Count() == 0)
                {
                    Debug.Write("No Capture Device");
                    return;
                }

                captureInitSettings.VideoDeviceId = devices[0].Id;

                capture = new MediaCapture();
                await capture.InitializeAsync(captureInitSettings);

                Debug.WriteLine("Video initialized.");

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private async void RecordingStart()
        {
            
            //Setup Recording File
            recordFile = await KnownFolders.VideosLibrary.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            MediaEncodingProfile recordProfile = null;
            recordProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Qvga);

            await capture.StartRecordToStorageFileAsync(recordProfile, recordFile);

            isRecording = true;
            Debug.WriteLine("Start Recoding");

            
        }

        /// <summary>
        /// 
        /// </summary>
        private async void RecordingStop()
        {
            await capture.StopRecordAsync();
            isRecording = false;
            Debug.WriteLine("Stop Recording");

        }

        /// <summary>
        /// 
        /// </summary>
        private async void Upload()
        {
            StorageCredentials sc = new StorageCredentials("securitywebcam", "[your storage key]");
            CloudStorageAccount storageAccount = new CloudStorageAccount(sc, true);

            //コンテナ設定
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("video");
            await container.CreateIfNotExistsAsync();

            //Blobアップロード
            string uploadFilename = "video" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".mp4";
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(uploadFilename);
            StorageFile storageFile = await KnownFolders.VideosLibrary.GetFileAsync("video.mp4");
            var stream = await storageFile.OpenAsync(FileAccessMode.Read);

            if (stream != null)
            {
                await blockBlob.UploadFromStreamAsync(stream);
            }

            //SendDM
            SendDm(uploadFilename);

            Debug.WriteLine("Upload Finish");

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, object e)
        {
            RecordingStop();
            timer.Stop();
            Upload();
        }

        
    }
}
