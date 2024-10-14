using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace UncoverGamesExporter.Services
{
    class TokenResponse
    {
        public string access_token;
        public int expires_in;
        public string refresh_token;
        public string scope;
        public string token_type;
    }

    class ListItem
    {
        public string size;
        public string id;
        public string name;
        public string createdTime;
        public string modifiedTime;
    }

    class ListResponse
    {
        public string nextPageToken;
        public ListItem[] files;
    }

    public delegate void Callback();

    internal class GoogleDrive
    {
        private HttpClient googleDriveClient;

        private string clientId;
        private string clientSecret;

        public void ConfigureClient(Configuration config, AppSettings appSettings)
        {
            this.clientId = appSettings.clientId;
            this.clientSecret = appSettings.clientSecret;
            this.SetClientToken();
        }

        public void SetClientToken()
        {
            Configuration config = Configuration.GetInstance();
            string token = config.GetToken();
            googleDriveClient = new HttpClient();
            googleDriveClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public bool TryBindListenerOnFreePort(out HttpListener httpListener, out string redirectURI)
        {
            // IANA suggested range for dynamic or private ports
            const int MinPort = 49215;
            const int MaxPort = 65535;

            for (int port = MinPort; port < MaxPort; port++)
            {
                redirectURI = string.Format("http://{0}:{1}/", IPAddress.Loopback, port);
                httpListener = new HttpListener();
                httpListener.Prefixes.Add(redirectURI);
                try
                {
                    httpListener.Start();
                    return true;
                }
                catch
                {
                    // nothing to do here -- the listener disposes itself when Start throws
                }
            }

            redirectURI = "";
            httpListener = null;
            return false;
        }

        public async void StartAuthentication(Callback callback)
        {
            HttpListener httpListener = null;
            string redirectURI = null;
            this.TryBindListenerOnFreePort(out httpListener, out redirectURI);

            string url = "https://accounts.google.com/o/oauth2/v2/auth"
                + "?client_id=" + this.clientId
                + "&redirect_uri=" + redirectURI
                + "&response_type=" + "code"
                + "&scope=" + "https://www.googleapis.com/auth/drive.appdata"
                + "&code_challenge=" + "ab23maopliamanw!@3asb"
                + "&code_challenge_method=" + "plain";
            Process.Start(url);

            // Waits for the OAuth authorization response.
            var context = await httpListener.GetContextAsync();

            // Sends an HTTP response to the browser.
            var response = context.Response;
            string responseString = string.Format("<html><head><meta http-equiv='refresh' content='10;url=https://google.com'></head><body>Please return to the app.</body></html>");
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            Task responseTask = responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
            {
                responseOutput.Close();
                httpListener.Stop();
                Console.WriteLine("HTTP server stopped.");
            });

            // Checks for errors.
            if (context.Request.QueryString.Get("error") != null)
            {
                return;
            }
            if (context.Request.QueryString.Get("code") == null)
            {
                return;
            }

            var code = context.Request.QueryString.Get("code");

            this.ContinueAuthentication(code, redirectURI, callback);
        }

        public async void ContinueAuthentication(string code, string redirectURI, Callback callback)
        {
            string url = "https://oauth2.googleapis.com/token";
            var dict = new Dictionary<string, string>();
            dict.Add("code", code);
            dict.Add("client_id", this.clientId);
            dict.Add("client_secret", this.clientSecret);
            dict.Add("redirect_uri", redirectURI);
            dict.Add("grant_type", "authorization_code");
            dict.Add("code_verifier", "ab23maopliamanw!@3asb");
            var client = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(dict) };
            var res = await client.SendAsync(req);

            var response = JsonConvert.DeserializeObject<TokenResponse>(res.Content.ReadAsStringAsync().Result);

            Configuration config = Configuration.GetInstance();
            config.SetToken(response.access_token);
            config.SetRefreshToken(response.refresh_token);
            config.SetExpiresIn(response.expires_in);

            callback();
        }

        public async Task<bool> RefreshToken()
        {
            Configuration config = Configuration.GetInstance();

            string url = "https://oauth2.googleapis.com/token";
            var dict = new Dictionary<string, string>();
            dict.Add("client_id", this.clientId);
            dict.Add("client_secret", this.clientSecret);
            dict.Add("refresh_token", config.GetRefreshToken());
            dict.Add("grant_type", "refresh_token");

            try
            {
                var client = new HttpClient();
                var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(dict) };
                var res = await client.SendAsync(req);

                var response = JsonConvert.DeserializeObject<TokenResponse>(res.Content.ReadAsStringAsync().Result);
                config.SetToken(response.access_token);
                config.SetExpiresIn(response.expires_in);
                this.SetClientToken();

                return true;
            }
            catch (HttpRequestException ex)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
            return false;
        }

        public async ValueTask<ListItem[]> ListFiles()
        {
            string pageToken = "first";
            ListItem[] files = new ListItem[0];
            while (pageToken != null)
            {
                string url = "https://www.googleapis.com/drive/v3/files?spaces=appDataFolder&fields=nextPageToken,files(id,name,createdTime,modifiedTime,size)&pageSize=1000";
                if (pageToken != "first")
                {
                    url += "&pageToken=" + pageToken;
                }
                var res = await googleDriveClient.GetAsync(url);
                var list = JsonConvert.DeserializeObject<ListResponse>(res.Content.ReadAsStringAsync().Result);
                
                // Merge new files with files array
                ListItem[] tempFiles = new ListItem[files.Length + list.files.Length];
                Array.Copy(files, tempFiles, files.Length);
                Array.Copy(list.files, 0, tempFiles, files.Length, list.files.Length);
                files = tempFiles;

                // Update pageToken (will be null if no more pages)
                pageToken = list.nextPageToken;
            }
            return files;
        }

        public async ValueTask<String> GetFileId(string name)
        {
            string url = "https://www.googleapis.com/drive/v3/files?spaces=appDataFolder&q=name%20%3D%20%27" + name + "%27";
            var res = await googleDriveClient.GetAsync(url);
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                await this.RefreshToken();
                res = await googleDriveClient.GetAsync(url);
            }

            var list = JsonConvert.DeserializeObject<ListResponse>(res.Content.ReadAsStringAsync().Result);
            return list.files.Length == 0
                ? ""
                : list.files[0].id;
        }

        public async Task<string> PrepareUpload(string fileName)
        {
            // Prepare Upload
            string prepareUrl = "https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable";
            string[] parents = new string[] { "appDataFolder" };

            string fileMetadata = JsonConvert.SerializeObject(new {
                name = fileName,
                parents = parents
            });

            HttpResponseMessage result = await googleDriveClient.PostAsync(prepareUrl, new StringContent(fileMetadata, Encoding.UTF8, "application/json"));

            return result.Headers.Location.ToString();
        }

        public async void UploadJson(string fileName, string jsonContent)
        {
            // TODO if request fails refresh token
            // TODO if file already exists and hasn't been updated, update file
            string uploadUrl = await PrepareUpload(fileName);

            HttpResponseMessage result = await googleDriveClient.PutAsync(uploadUrl, new StringContent(jsonContent, Encoding.UTF8, "application/json"));
        }

        public async Task<HttpResponseMessage> UploadAsset(string assetPath)
        {
            string fileName = assetPath.Replace('\\', '_');
            string uploadUrl = await PrepareUpload(fileName);

            // Create request from file stream
            string fullPath = Playnite.SDK.API.Instance.Database.GetFullFilePath(assetPath);
            FileStream stream = System.IO.File.OpenRead(fullPath);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Content = new StreamContent(stream);
            return await googleDriveClient.SendAsync(request);
        }

        public async Task<string> PrepareUpdate(string fileId)
        {
            // Prepare Upload
            string prepareUrl = "https://www.googleapis.com/upload/drive/v3/files/" + fileId + "?uploadType=resumable";
            string[] parents = new string[] { "appDataFolder" };

            string fileMetadata = JsonConvert.SerializeObject(new { parents });

            var req = new HttpRequestMessage(new HttpMethod("PATCH"), prepareUrl);
            HttpResponseMessage result = await googleDriveClient.SendAsync(req);

            return result.Headers.Location.ToString();
        }

        public async void UpdateJson(string fileId, string jsonContent)
        {
            string uploadUrl = await PrepareUpdate(fileId);

            HttpResponseMessage result = await googleDriveClient.PutAsync(uploadUrl, new StringContent(jsonContent, Encoding.UTF8, "application/json"));
        }
    }
}
