# Maca134.Arma.DllExport
Simplify C# extensions for ARMA

```
public class SomeClass
{
    [ArmaDllExport]
    public static string Invoke(string input, int size)
    {
        return input;
    }
}
```
