using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using System.Diagnostics;
using SmartTrafficBLE.Assets;
using Windows.System.Threading;
using Windows.Devices.Gpio;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace SmartTrafficBLE
{
    /// <summary>
    /// Main.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly BluetoothLEAdvertisementWatcher _watcher;

        static ConcurrentDictionary<string, ProximityBeaconFrame> dicVeiculos = new ConcurrentDictionary<string, ProximityBeaconFrame>();
        private GpioPinValue value1 = GpioPinValue.High;
        private GpioPinValue value2 = GpioPinValue.Low;

        private const int LED_pinVerde1 = 27; //Verde
        private const int LED_pinVervelho1 = 21; //Vermelho
        private const int LED_pinVervelho2 = 20; //Vermelho
        private const int LED_pinVerde2 = 18; //Verde 
        private const int LED_pinVerde3 = 17; //Verde
        private const int LED_pinVervelho3 = 26; //Vermelho
        private const int LED_pinVervelho4 = 19; //Vermelho
        private const int LED_pinVerde4 = 22; //Verde


        private GpioPin pinVerde1;
        private GpioPin pinVervelho1;
        private GpioPin pinVervelho2;
        private GpioPin pinVerde2;
        private GpioPin pinVerde3;
        private GpioPin pinVervelho3;
        private GpioPin pinVervelho4;
        private GpioPin pinVerde4;
        private ThreadPoolTimer timer;
        private ThreadPoolTimer timerNormalize;
        private ThreadPoolTimer removeDic;

        public MainPage()
        {
            this.InitializeComponent();

            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.Received += OnAdvertisementReceived;
            _watcher.ScanningMode = BluetoothLEScanningMode.Active;
            _watcher.Start();

            System.Diagnostics.Debug.WriteLine("\nBeacon Start");

            InitGPIO();
            timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(5));

            System.Diagnostics.Debug.WriteLine("\nGPIO Start");

            removeDic = ThreadPoolTimer.CreatePeriodicTimer(RemoveFromDic, TimeSpan.FromSeconds(1));
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            // Do whatever you want with the advertisement
            //Debug.WriteLine("\nBeacon Receveid with RSSI:" + eventArgs.RawSignalStrengthInDBm);

            if (eventArgs.Advertisement.ManufacturerData.Any())
            {

                foreach (var manufacturerData in eventArgs.Advertisement.ManufacturerData)
                {
                    // Print the company ID + the raw data in hex format
                    var manufacturerDataString = $"0x{manufacturerData.CompanyId.ToString("X")}: {BitConverter.ToString(manufacturerData.Data.ToArray())}";
                    //Debug.WriteLine("Manufacturer data: " + manufacturerDataString);

                    var manufacturerDataArry = manufacturerData.Data.ToArray();

                    if (IsProximityBeaconPayload(manufacturerData.CompanyId, manufacturerDataArry) && eventArgs.RawSignalStrengthInDBm > -60)
                    {
                        var beaconFrame = new ProximityBeaconFrame(manufacturerDataArry);
                        string id = ((ProximityBeaconFrame)beaconFrame).UuidAsString + '|' + eventArgs.BluetoothAddress;
                        beaconFrame.time = DateTime.Now;
                        if (!dicVeiculos.ContainsKey(id))
                        {
                            dicVeiculos.TryAdd(id, beaconFrame);
                            Debug.WriteLine("Incluiu");

                            timer.Cancel();
                            timer = ThreadPoolTimer.CreatePeriodicTimer(Amb_TickAsync, TimeSpan.FromSeconds(15));

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                TrafficStateValue.Text = "Operação Especial";
                            });

                            PiscaVerde();
                            PiscaVerde();
                            PiscaVerde();
                            PiscaVerde();
                            Debug.WriteLine("Muda Timer");
                            Debug.WriteLine("iBeacon Frame: " + BitConverter.ToString(manufacturerDataArry));

                            Debug.WriteLine("iBeacon UUID: " + ((ProximityBeaconFrame)beaconFrame).UuidAsString);
                            Debug.WriteLine("iBeacon Major: " + ((ProximityBeaconFrame)beaconFrame).Major);
                            Debug.WriteLine("iBeacon Minor: " + ((ProximityBeaconFrame)beaconFrame).Minor);

                            
                            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                listView1.Items.Add(id + "\nSinal: " + eventArgs.RawSignalStrengthInDBm
                                    + "\nData\\Hora: " + DateTime.Now);

                                listView1.Items.Remove("Nenhuma viatura próximo!");

                            });
                            
                        }

                    }

                }
            }

        }

        public static bool IsProximityBeaconPayload(ushort companyId, byte[] manufacturerData)
        {
            return companyId == 0x004C &&
                   manufacturerData.Length >= 23 &&
                   manufacturerData[0] == 0x02 &&
                   manufacturerData[1] == 0x15;
        }


        private void InitGPIO()
        {
            pinVerde1 = GpioController.GetDefault().OpenPin(LED_pinVerde1);
            pinVervelho1 = GpioController.GetDefault().OpenPin(LED_pinVervelho1);
            pinVervelho2 = GpioController.GetDefault().OpenPin(LED_pinVervelho2);
            pinVerde2 = GpioController.GetDefault().OpenPin(LED_pinVerde2);
            pinVerde3 = GpioController.GetDefault().OpenPin(LED_pinVerde3);
            pinVervelho3 = GpioController.GetDefault().OpenPin(LED_pinVervelho3);
            pinVervelho4 = GpioController.GetDefault().OpenPin(LED_pinVervelho4);
            pinVerde4 = GpioController.GetDefault().OpenPin(LED_pinVerde4);


            pinVerde1.Write(GpioPinValue.High);
            pinVervelho1.Write(GpioPinValue.Low);
            pinVervelho2.Write(GpioPinValue.High);
            pinVerde2.Write(GpioPinValue.Low);


            pinVerde1.SetDriveMode(GpioPinDriveMode.Output);
            pinVervelho1.SetDriveMode(GpioPinDriveMode.Output);
            pinVervelho2.SetDriveMode(GpioPinDriveMode.Output);
            pinVerde2.SetDriveMode(GpioPinDriveMode.Output);

            pinVerde3.Write(GpioPinValue.High);
            pinVervelho3.Write(GpioPinValue.Low);
            pinVervelho4.Write(GpioPinValue.High);
            pinVerde4.Write(GpioPinValue.Low);


            pinVerde3.SetDriveMode(GpioPinDriveMode.Output);
            pinVervelho3.SetDriveMode(GpioPinDriveMode.Output);
            pinVervelho4.SetDriveMode(GpioPinDriveMode.Output);
            pinVerde4.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {

            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TrafficStateValue.Text = "Operação Normal";
                listView1.Items.Clear();
                listView1.Items.Add("Nenhuma viatura próximo!");
            });

            Debug.WriteLine("Timer_Tick");
            value1 = (value1 == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;
            value2 = (value2 == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;

            pinVerde1.Write(value1);
            pinVervelho1.Write(value2);
            pinVervelho2.Write(value1);
            pinVerde2.Write(value2);

            Task.Delay(-1).Wait(1000);

            pinVerde3.Write(value1);
            pinVervelho3.Write(value2);
            pinVervelho4.Write(value1);
            pinVerde4.Write(value2);

        }

        private void Amb_TickAsync(ThreadPoolTimer timer)
        {
            Debug.WriteLine("Amb_Tick");

            value1 = (value1 == GpioPinValue.High) ? GpioPinValue.High : GpioPinValue.High;
            value2 = (value2 == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.Low;

            pinVerde1.Write(value1);
            pinVervelho1.Write(value2);
            pinVervelho2.Write(value1);
            pinVerde2.Write(value2);
            pinVerde3.Write(value1);
            pinVervelho3.Write(value2);
            pinVervelho4.Write(value1);
            pinVerde4.Write(value2);

            if (timerNormalize != null)
            {
                timerNormalize.Cancel();
            }
            timerNormalize = ThreadPoolTimer.CreatePeriodicTimer(Normalize, TimeSpan.FromSeconds(10));
        }

        private void Normalize(ThreadPoolTimer timer)
        {
            Debug.WriteLine("Normalize");

            timerNormalize.Cancel();
            this.timer.Cancel();
            this.timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(5));
            value1 = GpioPinValue.High;
            value2 = GpioPinValue.Low;

            pinVerde1.Write(value1);
            pinVervelho1.Write(value2);
            pinVervelho2.Write(value1);
            pinVerde2.Write(value2);

            pinVerde3.Write(value1);
            pinVervelho3.Write(value2);
            pinVervelho4.Write(value1);
            pinVerde4.Write(value2);

        }

        private void PiscaVerde()
        {
            Debug.WriteLine("PiscaVerde");
            Task.Delay(-1).Wait(500);
            pinVerde1.Write(GpioPinValue.Low);
            pinVervelho1.Write(GpioPinValue.Low);
            pinVervelho2.Write(GpioPinValue.High);
            pinVerde2.Write(GpioPinValue.Low);
            pinVerde3.Write(GpioPinValue.Low);
            pinVervelho3.Write(GpioPinValue.Low);
            pinVervelho4.Write(GpioPinValue.High);
            pinVerde4.Write(GpioPinValue.Low);


            Task.Delay(-1).Wait(500);
            pinVerde1.Write(GpioPinValue.High);
            pinVervelho1.Write(GpioPinValue.Low);
            pinVervelho2.Write(GpioPinValue.High);
            pinVerde2.Write(GpioPinValue.Low);
            pinVerde3.Write(GpioPinValue.High);
            pinVervelho3.Write(GpioPinValue.Low);
            pinVervelho4.Write(GpioPinValue.High);
            pinVerde4.Write(GpioPinValue.Low);

        }

        private void RemoveFromDic(ThreadPoolTimer timer)
        {  
            
            Debug.WriteLine("RemoveFromDic");
            foreach (string key in dicVeiculos.Keys)
            {
                ProximityBeaconFrame b = dicVeiculos[key];
                if (DateTime.Now.Subtract(b.time) > TimeSpan.FromSeconds(25))
                {
                    dicVeiculos.TryRemove(key, out b);
                    Debug.WriteLine("Removeu");
                }
                
            }

        }
    }
}