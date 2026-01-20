// Decompiled with JetBrains decompiler
// Type: vaurioajoneuvo_finder.Properties.Resources
// Assembly: vaurioajoneuvo_finder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D94E05F5-0C4E-483F-B409-5B78332FAE36
// Assembly location: C:\Users\andre\OneDrive\Pulpit\1\vaurioajoneuvo_finder.exe

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

#nullable disable
namespace vaurioajoneuvo_finder.Properties;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
  private static ResourceManager resourceMan;
  private static CultureInfo resourceCulture;

  internal Resources()
  {
  }

  [EditorBrowsable(EditorBrowsableState.Advanced)]
  internal static ResourceManager ResourceManager
  {
    get
    {
      if (vaurioajoneuvo_finder.Properties.Resources.resourceMan == null)
        vaurioajoneuvo_finder.Properties.Resources.resourceMan = new ResourceManager("vaurioajoneuvo_finder.Properties.Resources", typeof (vaurioajoneuvo_finder.Properties.Resources).Assembly);
      return vaurioajoneuvo_finder.Properties.Resources.resourceMan;
    }
  }

  [EditorBrowsable(EditorBrowsableState.Advanced)]
  internal static CultureInfo Culture
  {
    get => vaurioajoneuvo_finder.Properties.Resources.resourceCulture;
    set => vaurioajoneuvo_finder.Properties.Resources.resourceCulture = value;
  }

  internal static Icon icons8_car
  {
    get
    {
      return (Icon) vaurioajoneuvo_finder.Properties.Resources.ResourceManager.GetObject(nameof (icons8_car), vaurioajoneuvo_finder.Properties.Resources.resourceCulture);
    }
  }

  internal static Bitmap icons8_car_16
  {
    get
    {
      return (Bitmap) vaurioajoneuvo_finder.Properties.Resources.ResourceManager.GetObject(nameof (icons8_car_16), vaurioajoneuvo_finder.Properties.Resources.resourceCulture);
    }
  }
}
