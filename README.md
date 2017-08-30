# Maca134.Arma.DllExport - [Download](https://www.nuget.org/packages/Maca134.Arma.DllExport/)
Simplify C# extensions for ARMA

```PM> Install-Package Maca134.Arma.DllExport```

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
