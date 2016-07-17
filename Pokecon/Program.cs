using CommandLineParser.Arguments;
using Geocoding.Google;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Pokecon
{
    // https://www.reddit.com/r/pokemongodev/
    // https://github.com/tejado/pokemongo-api-demo

    public class Program
    {
        const string API_URL = "https://pgorelease.nianticlabs.com/plfe/rpc";
        const string LOGIN_URL = "https://sso.pokemon.com/sso/login?service=https%3A%2F%2Fsso.pokemon.com%2Fsso%2Foauth2.0%2FcallbackAuthorize";
        const string LOGIN_OAUTH = "https://sso.pokemon.com/sso/oauth2.0/accessToken";

        static bool _debug = true;
        static ulong COORDS_LATITUDE = f2i(38.8791981);
        static ulong COORDS_LONGITUDE = f2i(-76.9818437);
        static ulong COORDS_ALTITUDE = 0;

        static void set_location(string locationName)
        {
            var geocoder = new GoogleGeocoder() { };
            var loc = geocoder.Geocode(locationName).FirstOrDefault();
            Console.WriteLine("[!] Your given location: {0}", loc.FormattedAddress);
            Console.WriteLine("[!] lat/long/alt: {0} {1} {2}", loc.Coordinates.Latitude, loc.Coordinates.Longitude, 0);
            set_location_coords(loc.Coordinates.Latitude, loc.Coordinates.Longitude, 0);
        }

        static ulong f2i(double value) { return BitConverter.ToUInt64(BitConverter.GetBytes(value), 0); }

        static void set_location_coords(double latitude, double longitude, double alt)
        {
            COORDS_LATITUDE = f2i(latitude);
            COORDS_LONGITUDE = f2i(longitude);
            COORDS_ALTITUDE = f2i(alt);
        }

        static ResponseEnvelop api_req(string api_endpoint, string access_token, RequestEnvelop.Types.Requests[] reqs, string provider)
        {
            try
            {
                var envelop = new RequestEnvelop
                {
                    Unknown1 = 2,
                    RpcId = 8145806132888207460,
                    Latitude = COORDS_LATITUDE,
                    Longitude = COORDS_LONGITUDE,
                    Altitude = COORDS_ALTITUDE,
                    Unknown12 = 989,
                    Auth = new RequestEnvelop.Types.AuthInfo
                    {
                        Provider = provider,
                        Token = new RequestEnvelop.Types.AuthInfo.Types.JWT { Contents = access_token, Unknown13 = 59 },
                    }
                };
                foreach (var r in reqs)
                    envelop.Requests.Add(r);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Niantic App");
                    using (var ms = new MemoryStream())
                    {
                        envelop.WriteTo(ms);
                        ms.Position = 0;
                        var result = client.PostAsync(api_endpoint, new ByteArrayContent(ms.ToArray())).Result;
                        var r = result.Content.ReadAsByteArrayAsync().Result;
                        var ret = ResponseEnvelop.Parser.ParseFrom(r);
                        return ret;
                    }
                }
            }
            catch (Exception e)
            {
                if (_debug)
                    Console.WriteLine(e);
                return null;
            }
        }

        static string get_api_endpoint(string access_token, string provider)
        {
            var ret = api_req(API_URL, access_token, new[] {
                new RequestEnvelop.Types.Requests { Type = 2 },
                new RequestEnvelop.Types.Requests { Type = 126 },
                new RequestEnvelop.Types.Requests { Type = 4 },
                new RequestEnvelop.Types.Requests { Type = 129 },
                new RequestEnvelop.Types.Requests { Type = 5, Message = new RequestEnvelop.Types.Unknown3 { Unknown4 = "4a2e9bc330dae60e7b74fc85b98868ab4700802e" } },
            }, provider);
            try { return "https://" + ret.ApiUrl + "/rpc"; }
            catch (Exception e){
                return null;
            }
        }

        static ResponseEnvelop get_profile(string api_endpoint, string access_token, string provider)
        {
            return api_req(api_endpoint, access_token, new[] { new RequestEnvelop.Types.Requests { Type = 2 } }, provider);
        }

        static string login_google(string username, string password)
        {
            Console.WriteLine("[!] Google login for: {0}", username);
            var first = "https://accounts.google.com/o/oauth2/auth?client_id=848232511240-73ri3t7plvk96pj4f85uj8otdat2alem.apps.googleusercontent.com&redirect_uri=urn%3Aietf%3Awg%3Aoauth%3A2.0%3Aoob&response_type=code&scope=openid%20email%20https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fuserinfo.email";
            var second = "https://accounts.google.com/AccountLoginInfo";
            var third = "https://accounts.google.com/signin/challenge/sl/password";
            var last = "https://accounts.google.com/o/oauth2/token";
            using (var clientHandler = new HttpClientHandler())
            {
                clientHandler.AllowAutoRedirect = true;
                using (var client = new HttpClient(clientHandler))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (iPad; CPU OS 8_4 like Mac OS X) AppleWebKit/600.1.4 (KHTML, like Gecko) Mobile/12H143");
                    var response = client.GetAsync(first).Result;
                    var r = response.Content.ReadAsStringAsync().Result;

                    var galx_regex = "name=\"GALX\" value=\"(.*?)\"";
                    var matches = Regex.Matches(r, galx_regex);
                    var galx = matches[0].Groups[1].Value;
                    var gxf_regex = "name=\"gxf\" value=\"(.*?)\"";
                    var gxf = Regex.Matches(r, gxf_regex)[0].Groups[1].Value;
                    var cont_regex = "name=\"continue\" value=\"(.*?)\"";
                    matches = Regex.Matches(r, cont_regex);
                    var cont = matches[0].Groups[1].Value.Replace("&amp;", "&");
                    var data1 = new[]
                    {
                        new KeyValuePair<string, string>("Page", "PasswordSeparationSignIn"),
                        new KeyValuePair<string, string>("GALX", galx),
                        new KeyValuePair<string, string>("gxf", gxf),
                        new KeyValuePair<string, string>("continue", cont),
                        new KeyValuePair<string, string>("ltmpl", "embedded"),
                        new KeyValuePair<string, string>("scc", "1"),
                        new KeyValuePair<string, string>("sarp", "1"),
                        new KeyValuePair<string, string>("oauth", "1"),
                        new KeyValuePair<string, string>("ProfileInformation", ""),
                        new KeyValuePair<string, string>("_utf8", "?"),
                        new KeyValuePair<string, string>("bgresponse", "js_disabled"),
                        new KeyValuePair<string, string>("Email", username),
                        new KeyValuePair<string, string>("signIn", "Next"),
                    };
                    response = client.PostAsync(second, new FormUrlEncodedContent(data1)).Result;
                    r = response.Content.ReadAsStringAsync().Result;
                    gxf = Regex.Matches(r, gxf_regex)[0].Groups[1].Value;
                    var profileinformation_regex = "name=\"ProfileInformation\" type=\"hidden\" value=\"(.*?)\"";
                    var profileinformation = Regex.Matches(r, profileinformation_regex)[0].Groups[1].Value;
                    var data2 = new[]
                    {
                        new KeyValuePair<string, string>("Page", "PasswordSeparationSignIn"),
                        new KeyValuePair<string, string>("GALX", galx),
                        new KeyValuePair<string, string>("gxf", gxf),
                        new KeyValuePair<string, string>("continue", cont),
                        new KeyValuePair<string, string>("ltmpl", "embedded"),
                        new KeyValuePair<string, string>("scc", "1"),
                        new KeyValuePair<string, string>("sarp", "1"),
                        new KeyValuePair<string, string>("oauth", "1"),
                        new KeyValuePair<string, string>("ProfileInformation", profileinformation),
                        new KeyValuePair<string, string>("_utf8", "?"),
                        new KeyValuePair<string, string>("bgresponse", "js_disabled"),
                        new KeyValuePair<string, string>("Email", username),
                        new KeyValuePair<string, string>("Passwd", password),
                        new KeyValuePair<string, string>("signIn", "Sign in"),
                        new KeyValuePair<string, string>("PersistentCookie", "yes"),
                    };
                    response = client.PostAsync(third, new FormUrlEncodedContent(data2)).Result;
                    r = response.Content.ReadAsStringAsync().Result;
                    var clientid = "848232511240-73ri3t7plvk96pj4f85uj8otdat2alem.apps.googleusercontent.com";
                    var statewrapper_regex = "name=\"state_wrapper\" value=\"(.*?)\"";
                    var statewrapper = Regex.Matches(r, statewrapper_regex)[0].Groups[1].Value;
                    var connect_approve_regex = "id=\"connect-approve\" action=\"(.*?)\"";
                    var connect_approve = Regex.Matches(r, connect_approve_regex)[0].Groups[1].Value.Replace("&amp;", "&");
                    var data3 = new[]
                    {
                        new KeyValuePair<string, string>("submit_access", "true"),
                        new KeyValuePair<string, string>("state_wrapper", statewrapper),
                        new KeyValuePair<string, string>("_utf8", "?"),
                        new KeyValuePair<string, string>("bgresponse", "js_disabled"),
                    };
                    response = client.PostAsync(connect_approve, new FormUrlEncodedContent(data3)).Result;
                    r = response.Content.ReadAsStringAsync().Result;
                    var code_regex = "id=\"code\" type=\"text\" readonly=\"readonly\" value=\"(.*?)\"";
                    var code = Regex.Matches(r, code_regex)[0].Groups[1].Value.Replace("&amp;", "&");
                    var data4 = new[]
                    {
                        new KeyValuePair<string, string>("client_id", clientid),
                        new KeyValuePair<string, string>("client_secret", "NCjF1TLi2CcY6t5mt0ZveuL7"),
                        new KeyValuePair<string, string>("code", code),
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("redirect_uri", "urn:ietf:wg:oauth:2.0:oob"),
                        new KeyValuePair<string, string>("scope", "openid email https://www.googleapis.com/auth/userinfo.email"),
                    };
                    response = client.PostAsync(last, new FormUrlEncodedContent(data4)).Result;
                    r = response.Content.ReadAsStringAsync().Result;
                    var jdata = JObject.Parse(r);
                    return jdata["id_token"].ToString();
                }
            }
        }

        static string login_ptc(string username, string password)
        {
            Console.WriteLine("[!] login for: {0}", username);

            using (var clientHandler = new HttpClientHandler())
            {
                clientHandler.AllowAutoRedirect = false;
                using (var client = new HttpClient(clientHandler))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "niantic");
                    var r = client.GetAsync(LOGIN_URL).Result.Content.ReadAsStringAsync().Result;
                    var jdata = JObject.Parse(r);
                    var data = new[]
                    {
                        new KeyValuePair<string, string>("lt", (string)jdata["lt"]),
                        new KeyValuePair<string, string>("execution", (string)jdata["execution"]),
                        new KeyValuePair<string, string>("_eventId", "submit"),
                        new KeyValuePair<string, string>("username", username),
                        new KeyValuePair<string, string>("password", password),
                    };
                    var result = client.PostAsync(LOGIN_URL, new FormUrlEncodedContent(data)).Result;
                    if (result.Headers.Location == null)
                        return null;
                    var location = result.Headers.Location.ToString();
                    var r1 = result.Content.ReadAsStringAsync().Result;

                    string ticket = null;
                    try { ticket = new Regex(".*ticket=").Split(location)[1]; }
                    catch
                    {
                        if (_debug)
                            Console.WriteLine(r1);
                        return null;
                    }

                    var data1 = new[]
                    {
                        new KeyValuePair<string, string>("client_id", "mobile-app_pokemon-go"),
                        new KeyValuePair<string, string>("redirect_uri", "https://www.nianticlabs.com/pokemongo/error"),
                        new KeyValuePair<string, string>("client_secret", "w8ScCUXJQc6kXKw8FiOhd8Fixzht18Dq3PEVkUCP5ZPxtgyWsbTvWHFLm2wNY0JR"),
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("code", ticket),
                    };
                    var r2 = client.PostAsync(LOGIN_OAUTH, new FormUrlEncodedContent(data1)).Result.Content.ReadAsStringAsync().Result;
                    var access_token = new Regex("&expires.*").Split(r2)[0];
                    access_token = new Regex(".*access_token=").Split(access_token)[1];
                    return access_token;
                }
            }
        }

        public class Options
        {
            [ValueArgument(typeof(string), 'u', "username", Description = "Username", Optional = false)]
            public string username { get; set; }
            [ValueArgument(typeof(string), 'p', "password", Description = "Password", Optional = false)]
            public string password { get; set; }
            [ValueArgument(typeof(string), 'l', "location", Description = "Location", DefaultValue = "1600 pennsylvania ave washington dc")]
            public string location { get; set; }
            [ValueArgument(typeof(bool), 'd', "debug", Description = "Debug Mode", DefaultValue = true)]
            public bool debug { get; set; }
            [ValueArgument(typeof(string), 'm', "method", Description = "Login Method", DefaultValue = "ptc")]
            public string method { get; set; }

        }

        public static void Main(string[] args1)
        {
            var argsParser = new CommandLineParser.CommandLineParser();
            var args = new Options { };
            argsParser.ExtractArgumentAttributes(args);
            try { argsParser.ParseCommandLine(args1); }
            catch (Exception e) { Console.WriteLine(e.Message); return; }

            if (args.debug)
            {
                _debug = true;
                Console.WriteLine("[!] DEBUG mode on");
            }
            if (string.IsNullOrEmpty(args.username))
            {
                Console.WriteLine("[!] DEBUG mode on");
                return;
            }

            set_location(args.location);
            string access_token;
            if (args.method == "ptc")
            {
                access_token = login_ptc(args.username, args.password);
            }
            else if (args.method == "google")
            {
                access_token = login_google(args.username, args.password);
            }
            else
            {
                Console.WriteLine("[-] Invalid provider");
                return;
            }
            if (access_token == null)
            {
                Console.WriteLine("[-] Wrong username/password");
                return;
            }
            Console.WriteLine("[+] RPC Session Token: {0} ...", access_token);

            var api_endpoint = get_api_endpoint(access_token, args.method);
            if (api_endpoint == null)
            {
                Console.WriteLine("[-] RPC server offline");
                return;
            }
            Console.WriteLine("[+] Received API endpoint: {0}", api_endpoint);

            var profile = get_profile(api_endpoint, access_token, args.method);
            if (profile != null)
            {
                Console.WriteLine("[+] Login successful");
                var profile2 = profile.Payload[0].Profile;
                Console.WriteLine("[+] Username: {0}", profile2.Username);
                var creationTime = FromUnixTime(profile2.CreationTime);
                Console.WriteLine("[+] You are playing Pokemon Go since: {0}", creationTime);
                foreach (var curr in profile2.Currency)
                    Console.WriteLine("[+] {0}: {1}", curr.Type, curr.Amount);
            }
            else {
                Console.WriteLine("[-] Ooops...");
            }
            Console.ReadLine();
        }

        public static DateTime FromUnixTime(long source)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(source);
        }
    }
}
