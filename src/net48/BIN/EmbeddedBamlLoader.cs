using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Markup;
using System.Xaml;

namespace BIN;

/// <summary>
/// Loads decompiled WPF BAML files that are embedded as individual manifest
/// resources. Application.LoadComponent only searches Assembly.g.resources,
/// which is not present in the hot-reload development assembly.
/// </summary>
public static class EmbeddedBamlLoader
{
	public static void LoadComponent(object component, Uri resourceLocator)
	{
		if (component == null) throw new ArgumentNullException(nameof(component));
		if (resourceLocator == null) throw new ArgumentNullException(nameof(resourceLocator));

		Assembly assembly = component.GetType().Assembly;
		string resourceName = GetResourceName(resourceLocator);
		Stream stream = assembly.GetManifestResourceStream(resourceName);
		if (stream == null)
		{
			string actual = assembly.GetManifestResourceNames()
				.FirstOrDefault(name => string.Equals(name, resourceName, StringComparison.OrdinalIgnoreCase));
			if (actual != null) stream = assembly.GetManifestResourceStream(actual);
		}
		if (stream == null)
		{
			throw new IOException($"Cannot locate embedded BAML resource '{resourceName}' in '{assembly.Location}'.");
		}

		using (stream)
		{
			try
			{
				Assembly wpfAsm = typeof(System.Windows.Markup.XamlReader).Assembly;
				Type schemaCtxType = wpfAsm.GetType("System.Windows.Baml2006.Baml2006SchemaContext");
				ConstructorInfo sCtor = schemaCtxType?.GetConstructor(
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
					null,
					new[] { typeof(Assembly) },
					null);
				object schemaContext = sCtor?.Invoke(new object[] { assembly });

				MethodInfo createSettings = typeof(System.Windows.Markup.XamlReader).GetMethod("CreateBamlReaderSettings", BindingFlags.Static | BindingFlags.NonPublic);
				object bamlReaderSettings = createSettings?.Invoke(null, null);

				Uri baseUri = resourceLocator.IsAbsoluteUri
					? resourceLocator
					: new Uri(new Uri("pack://application:,,,/", UriKind.Absolute), resourceLocator);

				bamlReaderSettings?.GetType().GetProperty("BaseUri", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bamlReaderSettings, baseUri, null);
				bamlReaderSettings?.GetType().GetProperty("LocalAssembly", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bamlReaderSettings, assembly, null);
				bamlReaderSettings?.GetType().GetProperty("OwnsStream", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bamlReaderSettings, true, null);

				Type readerInternalType = wpfAsm.GetType("System.Windows.Baml2006.Baml2006ReaderInternal");
				ConstructorInfo ctor = null;
				if (readerInternalType != null)
				{
					foreach (ConstructorInfo c in readerInternalType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
					{
						ParameterInfo[] pars = c.GetParameters();
						if (pars.Length == 4) { ctor = c; break; }
					}
				}

				if (ctor != null && schemaContext != null && bamlReaderSettings != null)
				{
					System.Xaml.XamlReader reader = (System.Xaml.XamlReader)ctor.Invoke(new object[] { stream, schemaContext, bamlReaderSettings, component });

					MethodInfo createWriterSettings = typeof(System.Windows.Markup.XamlReader).GetMethod("CreateObjectWriterSettingsForBaml", BindingFlags.Static | BindingFlags.NonPublic);
					XamlObjectWriterSettings writerSettings = (XamlObjectWriterSettings)createWriterSettings?.Invoke(null, null) ?? new XamlObjectWriterSettings();
					writerSettings.RootObjectInstance = component;

					using (XamlObjectWriter writer = new XamlObjectWriter((XamlSchemaContext)schemaContext, writerSettings))
					{
						XamlServices.Transform(reader, writer);
					}
				}
				else
				{
					MethodInfo loadBaml = typeof(System.Windows.Markup.XamlReader).GetMethod(
						"LoadBaml",
						BindingFlags.Static | BindingFlags.NonPublic,
						null,
						new[] { typeof(Stream), typeof(ParserContext), typeof(object), typeof(bool) },
						null);
					if (loadBaml == null) throw new MissingMethodException(typeof(System.Windows.Markup.XamlReader).FullName, "LoadBaml");
					ParserContext context = new ParserContext { BaseUri = baseUri };
					SetParserContextProperty(context, "StreamCreatedAssembly", assembly);
					SetParserContextProperty(context, "RootElement", component);
					loadBaml.Invoke(null, new object[] { stream, context, component, true });
				}
			}
			catch (TargetInvocationException ex) when (ex.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				throw;
			}
		}
	}

	private static void SetParserContextProperty(ParserContext context, string name, object value)
	{
		PropertyInfo property = typeof(ParserContext).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
		property?.SetValue(context, value, null);
	}

	private static string GetResourceName(Uri resourceLocator)
	{
		string value = resourceLocator.OriginalString.Replace('\\', '/');
		const string marker = ";component/";
		int markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		if (markerIndex >= 0) value = value.Substring(markerIndex + marker.Length);
		value = value.TrimStart('/');
		value = Uri.UnescapeDataString(value);
		if (value.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
		{
			value = value.Substring(0, value.Length - 5) + ".baml";
		}
		return value;
	}
}
