using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.VisualElements;
using MahApps.Metro.Controls;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using SmartHomeMonitoringApp.Logics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SmartHomeMonitoringApp.Views
{
    /// <summary>
    /// RealTimeControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RealTimeControl : UserControl
    {

        public RealTimeControl()
        {
            InitializeComponent();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1); // 1초마다
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            LblSensingDt.Content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 습도를 나타낼 앵귤러차트를 초기화  , (앵귤러 차트를 그리기 위한 속성 )


            if(Commons.MQTT_CLIENT != null && Commons.MQTT_CLIENT.IsConnected)
            {  //이미 다른화면에서 MQTT를 연결했다면...
                Commons.MQTT_CLIENT.ApplicationMessageReceivedAsync += MQTT_CLIENT_ApplicationMessageReceivedAsync;
            }else
            {   //
                var mqttFactory = new MqttFactory();
                Commons.MQTT_CLIENT = mqttFactory.CreateMqttClient();
                var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer(Commons.BROKERHOST).Build();
                await Commons.MQTT_CLIENT.ConnectAsync(mqttClientOptions, CancellationToken.None);
                Commons.MQTT_CLIENT.ApplicationMessageReceivedAsync += MQTT_CLIENT_ApplicationMessageReceivedAsync;

                var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(
                    f =>
                    {
                        f.WithTopic(Commons.MQTTTOPIC);
                    }).Build();
                await Commons.MQTT_CLIENT.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            }
        }
       
        private Task MQTT_CLIENT_ApplicationMessageReceivedAsync(MQTTnet.Client.MqttApplicationMessageReceivedEventArgs arg)
        {
            var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
            Debug.WriteLine(payload);
            UpdateChart(payload);

            return Task.CompletedTask;
        }

       

        public IEnumerable<ISeries> HumidSeries { get; set; }

        public IEnumerable<VisualElement<SkiaSharpDrawingContext>> VisualElements { get; set; }

        public NeedleVisual Needle { get; set; }

        private static void SetStyle(
        double sectionsOuter, double sectionsWidth, PieSeries<ObservableValue> series)
        {
            series.OuterRadiusOffset = sectionsOuter;
            series.MaxRadialColumnWidth = sectionsWidth;
        }

        private void UpdateChart(string payload)
        {
            // 차트에 값 대입해서 차트가 나오도록
            this.Invoke(() =>
            {
                var currValue = JsonConvert.DeserializeObject<Dictionary<string, string>>(payload);
                
                var splitValue = currValue["VALUE"].Split('|');
                var temp = Convert.ToDouble(splitValue[0]);
                var humid = Convert.ToDouble(splitValue[1]);

                var tempVal = GaugeGenerator.BuildSolidGauge(new GaugeItem(
                    temp,
                    series =>
                    {
                        series.MaxRadialColumnWidth = 50;
                        series.DataLabelsSize = 50;
                    }
                    ));
                ChtLiVingTemp.Series = ChtDiningTemp.Series = ChtBedTemp.Series = ChtBathTemp.Series = tempVal;  // 차트 4개 다 같은 값을 할당

                var sectionOuter = 130;
                var sectionWidth = 20;



            // 습도차트 값 할당.
            HumidSeries = GaugeGenerator.BuildAngularGaugeSections(
                new GaugeItem(humid,
                s => SetStyle(sectionOuter, sectionWidth, s))
                );
               
                CHtLivingHumid.Series = ChtDiningHumid.Series = ChtBedHumid.Series = ChtBathHumid.Series = HumidSeries; // 차트랑

                // 습도를 나타낼 앵귤러(바늘)차트 초기화
                Needle = new NeedleVisual { Value = humid }; // 바늘의 시작 위치(기본 값)
                VisualElements = new VisualElement<SkiaSharpDrawingContext>[]
                {
                    new AngularTicksVisual
                    {
                        LabelsSize = 16,
                        LabelsOuterOffset = 15,
                        OuterOffset = 60,
                        TicksLength = 15
                    },
                    Needle
                };

                CHtLivingHumid.VisualElements = ChtDiningHumid.VisualElements = VisualElements;
                ChtBedHumid.VisualElements = ChtBathHumid.VisualElements = VisualElements; //위에서 만든 화면디자인을 차트에 적용
            });
        }

        private void BtnWarning_Click(object sender, RoutedEventArgs e)
        {
            Commons.MQTT_CLIENT.PublishStringAsync("pknu/rcv/", "{'control':'warning'}");
        }

        private void BtnNomal_Click(object sender, RoutedEventArgs e)
        {
            Commons.MQTT_CLIENT.PublishStringAsync("pknu/rcv/", "{'control':'normal'}");
        }

        private void BtnOff_Click(object sender, RoutedEventArgs e)
        {
            Commons.MQTT_CLIENT.PublishStringAsync("pknu/rcv/", "{'control':'off'}");
        }
    }
}
