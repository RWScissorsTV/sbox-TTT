using System;
using System.IO;
using System.Linq;
using System.Reflection;

var outputDir = Path.GetFullPath(@"C:\Program Files (x86)\Steam\steamapps\common\sbox\.vs\output");
var names = new[] { "Event", "GameEvent", "Rpc", "NetPermission", "NetFlags", "Slider2D", "Slider", "CitizenAnimation", "CitizenAnimationHelper", "PhysicsJoint", "Panel", "WorldPanel", "TextEntry", "IClient", "MenuSystem", "GameMenu", "ILoadingScreenPanel", "NavHostPanel", "IGameMenuPanel" };
foreach (var path in Directory.EnumerateFiles(outputDir, "*.dll"))
{
    try
    {
        var assembly = Assembly.LoadFrom(path);
        var types = assembly.GetTypes();
        foreach (var name in names)
        {
            var found = types.Where(t => t.Name == name || (t.FullName != null && t.FullName.EndsWith("." + name))).ToArray();
            if (found.Any())
            {
                Console.WriteLine($"{Path.GetFileName(path)} contains {name}: {found.Length}");
                foreach (var type in found.Take(10))
                    Console.WriteLine("  " + type.FullName);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed load {Path.GetFileName(path)}: {ex.Message}");
    }
}

var enginePath = Path.Combine(outputDir, "Sandbox.Engine.dll");
var engineAsm = Assembly.LoadFrom(enginePath);
var netFlagsType = engineAsm.GetType("Sandbox.NetFlags");
if (netFlagsType != null)
{
    Console.WriteLine($"Sandbox.NetFlags members:");
    foreach (var field in netFlagsType.GetFields(BindingFlags.Public | BindingFlags.Static))
        Console.WriteLine($"  Field: {field.Name} {field.FieldType}");
}
