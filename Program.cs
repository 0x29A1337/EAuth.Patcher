using HarmonyLib;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
class Program
{
    private static string APP_SECRET = "ENTER_APP_SECRET";

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No assembly path provided.");
            return;
        }

        Assembly assembly = null;
        try
        {
            assembly = Assembly.LoadFrom(args[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading assembly: {ex.Message}");
            return;
        }
   
        var harmony = new Harmony("_EAuth.Patcher");
      
        var originalMethod = typeof(HttpClient).GetMethod(
            nameof(HttpClient.SendAsync),
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new Type[] { typeof(HttpRequestMessage) },
            null
        );

        var prefixMethod = typeof(Program).GetMethod(nameof(PatchSendAsync));
        harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
       
        
        var entryPoint = assembly.EntryPoint;
        if (entryPoint == null)
        {
            Console.WriteLine("Entry point not found in the loaded assembly.");
            return;
        }

        try
        {
            var parameters = entryPoint.GetParameters().Length > 0 ? new object[] { new string[1] } : new object[0];
            var instance = entryPoint.IsStatic ? null : Activator.CreateInstance(entryPoint.DeclaringType);

            entryPoint.Invoke(instance, parameters);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error invoking entry point: {ex}");
            return;
        }
        Console.ReadKey();
    }

    public static bool PatchSendAsync(ref Task<HttpResponseMessage> __result, HttpRequestMessage request)
    {
        try
        {
            if (request.RequestUri == null || !request.RequestUri.ToString().Contains("eauth.us.to"))
                return true;
            if (request.Content != null)
            {
                string requestBody = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (requestBody.Contains("\"type\":\"login\"") || requestBody.Contains("\"type\": \"login\""))
                {
                    string interceptedPair = request.Headers.UserAgent?.ToString();
                    if (string.IsNullOrEmpty(interceptedPair)) interceptedPair = "signature_missing";

                    var hwidMatch = Regex.Match(requestBody, "\"hwid\"\\s*:\\s*\"([^\"]+)\"");
                    string interceptedHwid = hwidMatch.Success ? hwidMatch.Groups[1].Value : "bypass_hwid";

                    string fullJson = "{" +
                                      "\"message\":\"login_success\"," +
                                      "\"rank\":\"test\"," +
                                      "\"register_date\":\"2025-01-19\"," +
                                      "\"expire_date\":\"2099-12-31\"," +
                                      $"\"hwid\":\"{interceptedHwid}\"," +
                                      $"\"pair\":\"{interceptedPair}\"" +
                                      "}";

                    string dataToSign = APP_SECRET + "login_success" + fullJson;
                    string validHash = ComputeSHA512(dataToSign);
                    var fakeResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(fullJson, Encoding.UTF8, "application/json")
                    };
                    fakeResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    fakeResponse.Headers.Add("Eauth", validHash);
                    __result = Task.FromResult(fakeResponse);
                    return false; 
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hata] {ex.Message}");
            return true;
        }

        return true;
    }

    public static string ComputeSHA512(string input)
    {
        using (SHA512 sha512 = SHA512.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = sha512.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

}
