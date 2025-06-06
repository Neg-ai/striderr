// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

using Xunit;

using Stride.Core.Mathematics;
using Stride.Core.TypeConverters;
using Half = Stride.Core.Mathematics.Half;

namespace Stride.Core.Design.Tests;

/// <summary>
/// Tests for the <see cref="TypeConverters"/> classes.
/// </summary>
public class TestTypeConverter
{
    static TestTypeConverter()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(BaseConverter).Assembly.ManifestModule.ModuleHandle);
    }

    [Fact]
    public void TestColor()
    {
        TestConversionMultipleCultures(new Color(10, 20, 30, 40));
    }

    [Fact]
    public void TestColor3()
    {
        TestConversionMultipleCultures(new Color3(0.25f, 50.0f, -4.9f));
    }

    [Fact]
    public void TestColor4()
    {
        TestConversionMultipleCultures(new Color4(0.25f, 50.0f, -4.9f, 1));
    }

    [Fact]
    public void TestHalf()
    {
        TestConversionMultipleCultures(new Half(5.6f));
    }

    [Fact]
    public void TestHalf2()
    {
        TestConversionMultipleCultures(new Half2(new Half(5.12f), new Half(2)));
    }

    [Fact]
    public void TestHalf3()
    {
        TestConversionMultipleCultures(new Half3(new Half(5.12f), new Half(2), new Half(-17.54f)));
    }

    [Fact]
    public void TestHalf4()
    {
        TestConversionMultipleCultures(new Half4(new Half(5.12f), new Half(2), new Half(-17.54f), new Half(-5)));
    }

    [Fact]
    public void TestMatrix()
    {
        TestConversionMultipleCultures(new Matrix(0.25f, 50.0f, -4.9f, 1, 5.12f, 2, -17.54f, -5, 10.25f, 150.0f, -14.9f, 11, 15.12f, 12, -117.54f, -15));
    }

    [Fact]
    public void TestQuaternion()
    {
        TestConversionMultipleCultures(new Quaternion(5.12f, 2, -17.54f, -5));
    }

    [Fact]
    public void TestVector2()
    {
        TestConversionMultipleCultures(new Vector2(5.12f, -17.54f));
    }

    [Fact]
    public void TestVector3()
    {
        TestConversionMultipleCultures(new Vector3(5.12f, 2, -17.54f));
    }

    [Fact]
    public void TestVector4()
    {
        TestConversionMultipleCultures(new Vector4(5.12f, 2, -17.54f, -5));
    }

    private static void TestConversionMultipleCultures<T>(T testValue)
        where T : struct
    {
        string[] supportedLanguages = ["de-DE", "en-US", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "ru-RU", "zh-Hans"];
        foreach (var lang in supportedLanguages)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(lang);
            TestConversion(testValue);
        }
    }

    private static void TestConversion<T>(T testValue)
        where T : struct
    {
        var converter = TypeDescriptor.GetConverter(typeof(T));
        Assert.NotNull(converter);

        // Initial conversion
        Assert.True(converter.CanConvertTo(typeof(string)));
        var value = converter.ConvertTo(testValue, typeof(string));
        Assert.Equal(testValue.ToString(), value);

        // Reverse conversion
        Assert.True(converter.CanConvertFrom(typeof(string)));
        var result = converter.ConvertFrom(value!);
        Assert.Equal(testValue, result);
    }
}
