// Decompiled with JetBrains decompiler
// Type: vaurioajoneuvo_finder.Program
// Assembly: vaurioajoneuvo_finder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D94E05F5-0C4E-483F-B409-5B78332FAE36
// Assembly location: C:\Users\andre\OneDrive\Pulpit\1\vaurioajoneuvo_finder.exe

using System;
using System.Windows.Forms;

#nullable disable
namespace vaurioajoneuvo_finder;

internal static class Program
{
  [STAThread]
  private static void Main()
  {
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run((Form) new Form1());
  }
}
