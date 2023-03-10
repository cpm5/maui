#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Xaml;

namespace Microsoft.Maui.Controls
{
	/// <include file="../../docs/Microsoft.Maui.Controls/Setter.xml" path="Type[@FullName='Microsoft.Maui.Controls.Setter']/Docs/*" />
	[ContentProperty(nameof(Value))]
	[ProvideCompiled("Microsoft.Maui.Controls.XamlC.SetterValueProvider")]
	public sealed class Setter : IValueProvider
	{
		readonly ConditionalWeakTable<BindableObject, object> _originalValues = new ConditionalWeakTable<BindableObject, object>();

		/// <include file="../../docs/Microsoft.Maui.Controls/Setter.xml" path="//Member[@MemberName='TargetName']/Docs/*" />
		public string TargetName { get; set; }

		/// <include file="../../docs/Microsoft.Maui.Controls/Setter.xml" path="//Member[@MemberName='Property']/Docs/*" />
		public BindableProperty Property { get; set; }

		/// <include file="../../docs/Microsoft.Maui.Controls/Setter.xml" path="//Member[@MemberName='Value']/Docs/*" />
		public object Value { get; set; }

		object IValueProvider.ProvideValue(IServiceProvider serviceProvider)
		{
			if (Property == null)
				throw new XamlParseException("Property not set", serviceProvider);
			var valueconverter = serviceProvider.GetService(typeof(IValueConverterProvider)) as IValueConverterProvider;

			MemberInfo minforetriever()
			{
				MemberInfo minfo = null;
				try
				{
					minfo = Property.DeclaringType.GetRuntimeProperty(Property.PropertyName);
				}
				catch (AmbiguousMatchException e)
				{
					throw new XamlParseException($"Multiple properties with name '{Property.DeclaringType}.{Property.PropertyName}' found.", serviceProvider, innerException: e);
				}
				if (minfo != null)
					return minfo;
				try
				{
					return Property.DeclaringType.GetRuntimeMethod("Get" + Property.PropertyName, new[] { typeof(BindableObject) });
				}
				catch (AmbiguousMatchException e)
				{
					throw new XamlParseException($"Multiple methods with name '{Property.DeclaringType}.Get{Property.PropertyName}' found.", serviceProvider, innerException: e);
				}
			}

			object value = valueconverter.Convert(Value, Property.ReturnType, minforetriever, serviceProvider);
			Value = value;
			return this;
		}



		internal void Apply(BindableObject target, SetterSpecificity specificity)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));

			var targetObject = target;

			if (!string.IsNullOrEmpty(TargetName) && target is Element element)
				targetObject = element.FindByName(TargetName) as BindableObject ?? throw new XamlParseException($"Can not resole '{TargetName}' as Setter Target for '{target}'.");

			if (Property == null)
				return;

			object originalValue = targetObject.GetValue(Property);
			if (!Equals(originalValue, Property.DefaultValue))
			{
				_originalValues.Remove(targetObject);
				_originalValues.Add(targetObject, originalValue);
			}

			var value = Value;
			if (Value is IList<VisualStateGroup> visualStateGroupCollection)
				value = visualStateGroupCollection.Clone();

			//FIXME: use Specificity everywhere
			var fromStyle = specificity.Style > 0;
			if (Value is BindingBase binding)
				targetObject.SetBinding(Property, binding.Clone(), fromStyle);
			else if (Value is DynamicResource dynamicResource)
				targetObject.SetDynamicResource(Property, dynamicResource.Key, fromStyle);
			else
			{
				targetObject.SetValue(Property, Value, specificity: specificity);
			}
		}

		internal void UnApply(BindableObject target, bool fromStyle = false)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));

			var targetObject = target;

			if (!string.IsNullOrEmpty(TargetName) && target is Element element)
				targetObject = element.FindByName(TargetName) as BindableObject ?? throw new ArgumentNullException(nameof(targetObject));

			if (Property == null)
				return;

			object actual = targetObject.GetValue(Property);
			if (!Equals(actual, Value) && Value is not BindingBase && Value is not DynamicResource)
			{

				//Do not reset default value if the value has been changed
				_originalValues.Remove(targetObject);
				return;
			}

			if (_originalValues.TryGetValue(targetObject, out object defaultValue))
			{
				//FIXME: unapply no longer need specificity
				var specificity = fromStyle ? new SetterSpecificity(100, 0, 0, 0) : SetterSpecificity.VisualStateSetter;
				//reset default value, unapply bindings and dynamicResource
				targetObject.SetValue(Property, defaultValue, specificity);
				_originalValues.Remove(targetObject);
			}
			else
				//FIXME
				targetObject.ClearValue(Property, new SetterSpecificity(100, 0, 0, 0));
		}
	}

	internal readonly struct SetterSpecificity : IComparable<SetterSpecificity>
	{
		public static readonly SetterSpecificity FromHandler = new SetterSpecificity();
		public static readonly SetterSpecificity VisualStateSetter = new SetterSpecificity(1, 0, 0, 0, 0, 0, 0);
		public static readonly SetterSpecificity ManualValueSetter = new SetterSpecificity(0, 1, 0, 0, 0, 0, 0);
		public static readonly SetterSpecificity DynamicResourceSetter = new SetterSpecificity(0, 1, 0, 0, 0, 0, 0);

		//100-n: direct VSM (not from Style), n = max(99, distance between the RD and the target)
		public int Vsm { get; }

		//1: SetValue, SetBinding
		public int Manual { get; }

		//1: DynamicResource
		public int DynamicResource { get; }

		//XAML Style specificty
		//100-n: implicit style, n = max(99, distance between the RD and the target)
		//200-n: RD Style, n = max(99, distance between the RD and the target)
		//200: local style, inline css,
		//300-n: VSM, n = max(99, distance between the RD and the target)
		//300: !important (not implemented)
		public int Style { get; }

		//CSS Specificity, see https://developer.mozilla.org/en-US/docs/Web/CSS/Specificity
		public int Id { get; }
		public int Class { get; }
		public int Type { get; }

		SetterSpecificity(int vsm, int manual, int dynamicresource, int style, int id, int @class, int type)
		{
			Vsm = vsm;
			Manual = manual;
			DynamicResource = dynamicresource;
			Style = style;
			Id = id;
			Class = @class;
			Type = type;
		}

		public SetterSpecificity(int style, int id, int @class, int type) : this(0, 0, 0, style, id, @class, type)
		{
		}

		public int CompareTo(SetterSpecificity other)
		{
			if (Vsm != other.Vsm)
				return Vsm.CompareTo(other.Vsm);
			if (Manual != other.Manual)
				return Manual.CompareTo(other.Manual);
			if (DynamicResource != other.DynamicResource)
				return DynamicResource.CompareTo(other.DynamicResource);
			if (Style != other.Style)
				return Style.CompareTo(other.Style);
			if (Id != other.Id)
				return Id.CompareTo(other.Id);
			if (Class != other.Class)
				return Class.CompareTo(other.Class);
			return Type.CompareTo(other.Type);
		}
	}
}