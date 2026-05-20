using System;
using System.Linq;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using System.Reflection;
Console.WriteLine(typeof(DicomClient).Assembly.FullName);
foreach (var t in typeof(DicomClient).Assembly.GetTypes().Where(t => t.FullName!.Contains("Tls") || t.FullName!.Contains("Ssl") || t.FullName!.Contains("Certificate") || t.FullName!.Contains("Network")))
{
    Console.WriteLine(t.FullName);
    foreach (var p in t.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Take(8)) Console.WriteLine("  P " + p.PropertyType.Name + " " + p.Name);
    foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Where(m => m.Name.Contains("Certificate") || m.Name.Contains("Tls") || m.Name.Contains("Ssl")).Take(8)) Console.WriteLine("  M " + m);
}
