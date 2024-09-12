using System;
using System.Net.Http;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel;
using Newtonsoft.Json.Linq;
using Windows.UI.Xaml.Input;
using Windows.Storage;

namespace LinkShortener
{
    public sealed partial class MainPage : Page
    {
        string link;
        public string apiUrl = "https://short.marmak.net.pl/api";

        public MainPage()
        {
            InitializeComponent();
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue("apiURL", out object savedURL))
            {
                apiUrl = (string)savedURL;
            }
            RefreshStats();
            DisplayAppVersion();
            //DisplayServerVersion();
        }

        private async void RefreshStats()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl + "/stats");

                    string resultJson = await response.Content.ReadAsStringAsync();
                    JObject apiResponse = JObject.Parse(resultJson);
                    string links = apiResponse.Value<string>("linkCount");
                    string clicks = apiResponse.Value<string>("totalClicks");
                    string serverVersion = apiResponse.Value<string>("version");

                    ServerVersionLabel.Text = $"Server version: {serverVersion}";

                    LinkCountLabel.Text = $"Links in database: {links}";
                    TotalClicksLabel.Text = $"Total clicks: {clicks}";
                }
            }
            catch (Exception)
            {
                LinkCountLabel.Text = "Links in database: N/A";
                TotalClicksLabel.Text = "Total clicks: N/A";
                ServerVersionLabel.Text = "Server version: N/A";
            }
        }

        private void DisplayAppVersion()
        {
            Package package = Package.Current;

            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            string appVersion = $"{version.Major}.{version.Minor}.{version.Build}";
            AppVersionLabel.Text = $"App Version: {appVersion}";
        }

        //private async void DisplayServerVersion()
        //{
        //    try
        //    {
        //        using (HttpClient client = new HttpClient())
        //        {
        //            HttpResponseMessage response = await client.GetAsync(apiUrl + "/version");

        //            string resultJson = await response.Content.ReadAsStringAsync();
        //            JObject apiResponse = JObject.Parse(resultJson);
        //            string serverVersion = apiResponse.Value<string>("version");

        //            ServerVersionLabel.Text = $"Server version: {serverVersion}";
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        ServerVersionLabel.Text = "Server version: N/A";
        //    }
        //}

        private async void ButtonShorten_Click(object sender, RoutedEventArgs e)
        {
            string url = TextURL.Text;

            if (string.IsNullOrEmpty(url))
                return;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {

                    StringContent content = new StringContent($"{{\"url\": \"{url}\"}}", System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl + "/url", content);

                    string resultJson = await response.Content.ReadAsStringAsync();
                    JObject apiResponse = JObject.Parse(resultJson);
                    string error = apiResponse.Value<string>("error");

                    if (!string.IsNullOrEmpty(error))
                    {
                        MessageDialog dialog = new MessageDialog(error, "Error");
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        ResultLabel.Tag = apiResponse.Value<string>("link");
                        ResultLabel.Text = $"Your shortened link has been generated:\r\n {apiResponse.Value<string>("link")}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog(ex.Message, "Error");
                await dialog.ShowAsync();
            }
        }

        private async void ResultLabel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            TextBlock block = (TextBlock)sender;
            if (block.Tag == null)
                return;
            dataPackage.SetText((string)block.Tag);

            Clipboard.SetContent(dataPackage);

            MessageDialog dialog = new MessageDialog("Copied to clipboard!", "Information");
            await dialog.ShowAsync();
        }

        private async void CheckResultLabel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(link);

            Clipboard.SetContent(dataPackage);

            MessageDialog dialog = new MessageDialog("Copied to clipboard!", "Information");
            await dialog.ShowAsync();
        }

        private async void ButtonCheckUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = TextCheckURL.Text;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }

            if (string.IsNullOrEmpty(url))
                return;

            try
            {
                Uri uri = new Uri(url);

                string id = uri.Segments[uri.Segments.Length - 1];

                if (string.IsNullOrEmpty(id))
                {
                    MessageDialog dialog = new MessageDialog("Invalid URL", "Error");
                    await dialog.ShowAsync();
                    return;
                }

                using (HttpClient client = new HttpClient())
                {
                    string apiUrlWithId = $"{apiUrl}/url?id={id}";

                    HttpResponseMessage response = await client.GetAsync(apiUrlWithId);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultJson = await response.Content.ReadAsStringAsync();
                        JObject apiResponse = JObject.Parse(resultJson);
                        string error = apiResponse.Value<string>("error");

                        if (!string.IsNullOrEmpty(error))
                        {
                            MessageDialog dialog = new MessageDialog(error, "Error");
                            await dialog.ShowAsync();
                        }
                        else
                        {
                            link = apiResponse.Value<string>("url");
                            CheckResultLabel.Text = $"This URL leads to:\r\n {link}";
                        }
                    }
                    else
                    {
                        MessageDialog dialog = new MessageDialog($"Error: {response.StatusCode}", "Error");
                        await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog(ex.Message, "Error");
                await dialog.ShowAsync();
            }
        }

        private async void ChangeServerUrlButton_Click(object sender, RoutedEventArgs e)
        {
            InputScope scope = new InputScope();
            InputScopeName scopeName = new InputScopeName();
            scopeName.NameValue = InputScopeNameValue.Url;
            scope.Names.Add(scopeName);

            TextBox input = new TextBox()
            {
                Height = (double)Application.Current.Resources
                ["TextControlThemeMinHeight"],
                InputScope = scope,
                PlaceholderText = "Enter URL (example: https://short.marmak.net.pl/api)",
                Text = apiUrl
            };
            ContentDialog dialog = new ContentDialog()
            {
                Title = "Enter server URL",
                MaxWidth = ActualWidth,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                Content = input
            };
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                input = (TextBox)dialog.Content;
                apiUrl = input.Text;
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["apiURL"] = apiUrl;
                RefreshStats();
                //DisplayServerVersion();
            }
        }
        
        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            if (ResultLabel.Tag != null)
                DataTransferManager.ShowShareUI();
            else
            {
                MessageDialog dialog = new MessageDialog("Please shorten a link first", "Information");
                dialog.ShowAsync();
                return;
            }
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest request = args.Request;
            if (ResultLabel.Tag != null)
                request.Data.SetWebLink(new Uri((string)ResultLabel.Tag));
            else
            {
                return;
            }

            request.Data.Properties.Title = "Shortened link";
            request.Data.Properties.ApplicationName = "LinkShortener";
        }

        private void RefreshStatsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshStats();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Current.Bounds.Width < 600)
            {
                MainSplitView.OpenPaneLength = Window.Current.Bounds.Width;
            }
            else
            {
                MainSplitView.OpenPaneLength = 300;
            }
            MainSplitView.IsPaneOpen = true;
        }
    }
}