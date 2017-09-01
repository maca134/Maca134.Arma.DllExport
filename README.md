# Maca134.Arma.DllExport - [Download](https://www.nuget.org/packages/Maca134.Arma.DllExport/)
Simplify C# extensions for ARMA

```PM> Install-Package Maca134.Arma.DllExport```

[![Demo](https://img.youtube.com/vi/MXRBckxwqEw/0.jpg)](http://www.youtube.com/watch?v=MXRBckxwqEw)

```csharp
public class SomeClass
{
    [ArmaDllExport]
    public static string Invoke(string input, int size)
    {
        return input;
    }
}
```
