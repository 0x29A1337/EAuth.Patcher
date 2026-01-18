using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
class Program
{
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

        var originalMethod = typeof(JsonElement).GetMethod(
                  "GetProperty",            
                  new Type[] { typeof(string) }             
              );
      
        var prefixPatch = typeof(Program).GetMethod(
                 nameof(Patch),                       
                 BindingFlags.Static | BindingFlags.Public   
             );

        harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixPatch));


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

    public static bool Patch(ref JsonElement __instance, string propertyName, ref JsonElement __result)
    {
        if (propertyName == "message")
        {
            if (__instance.TryGetProperty("message", out JsonElement realProp))
            {
                string realValue = realProp.GetString();
                if (realValue == "key_unavailable" ||
                    realValue == "account_unavailable"  ||
                    realValue == "hwid_incorrect" ||
                    realValue == "subscription_expired" ||
                    realValue == "user_is_banned" ||
                    realValue == "account_unavailable"  ||
                    realValue == "session_expired"  ||
                    realValue == "session_overcrowded"  ||
                    realValue == "session_already_used")
                {
                    __result = Create("login_success");
                    return false;
                }

                return true;
            }
        }

        if (propertyName == "rank")
        {
            __result = Create("test");
            return false;
        }

        if (propertyName == "hwid")
        {
            __result = Create("test");
            return false;
        }

        if (propertyName == "expire_date")
        {
            __result = Create(DateTime.MaxValue.ToString());
            return false;
        }

        if (propertyName == "register_date")
        {
            __result = Create(DateTime.Now.ToString());
            return false;
        }

        if (__instance.TryGetProperty(propertyName, out JsonElement originalValue))
        {
            __result = originalValue;
            return false;
        }
        return false;
    }

    private static JsonElement Create(string value)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;
    }
}