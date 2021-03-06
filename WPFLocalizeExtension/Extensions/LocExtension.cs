﻿#region Copyright information
// <copyright file="LocExtension.cs">
//     Licensed under Microsoft Public License (Ms-PL)
//     http://wpflocalizeextension.codeplex.com/license
// </copyright>
// <author>Bernhard Millauer</author>
// <author>Uwe Mayer</author>
#endregion

#if SILVERLIGHT
namespace SLLocalizeExtension.Extensions
#else
namespace WPFLocalizeExtension.Extensions
#endif
{
    #region Uses
    using System;
    using System.Windows.Markup;
    using System.Windows;
    using System.Reflection;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Globalization;
    using System.Collections.Generic;
    using System.Windows.Data;
#if SILVERLIGHT
    using SLLocalizeExtension.Engine;
    using SLLocalizeExtension.TypeConverters;
    using SLLocalizeExtension.Providers;
#else
    using WPFLocalizeExtension.Engine;
    using WPFLocalizeExtension.TypeConverters;
    using WPFLocalizeExtension.Providers;
#endif
    using XAMLMarkupExtensions.Base;
    using System.Collections;
    using System.Windows.Media.Media3D;
    #endregion

    /// <summary>
    /// A generic localization extension.
    /// </summary>
    [ContentProperty("ResourceIdentifierKey")]
    public class LocExtension : NestedMarkupExtension, INotifyPropertyChanged, IDictionaryEventListener, IDisposable
    {
        #region PropertyChanged Logic
        /// <summary>
        /// Informiert über sich ändernde Eigenschaften.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notify that a property has changed
        /// </summary>
        /// <param name="property">
        /// The property that changed
        /// </param>
        internal void OnNotifyPropertyChanged(string property)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
        #endregion

        #region Variables
        private static Dictionary<string, object> ResourceBuffer = new Dictionary<string, object>();

        /// <summary>
        /// Holds the name of the Assembly where the .resx is located
        /// </summary>
        private string assembly;

        /// <summary>
        /// Holds the Name of the .resx dictionary.
        /// If it's null, "Resources" will get returned
        /// </summary>
        private string dict;

        /// <summary>
        /// Holds the Key to a .resx object
        /// </summary>
        private string key;

        /// <summary>
        /// A custom converter, supplied in the XAML code.
        /// </summary>
        private IValueConverter converter = null;

        /// <summary>
        /// A parameter that can be supplied along with the converter object.
        /// </summary>
        private object converterParameter = null;
        #endregion

        /// <summary>
        /// Clears the common resource buffer.
        /// </summary>
        internal static void ClearResourceBuffer()
        {
            if (ResourceBuffer != null)
                ResourceBuffer.Clear();
            ResourceBuffer = null;
        }

        #region Properties
        /// <summary>
        /// Gets or sets the Key to a .resx object
        /// </summary>
        public string Key
        {
            get
            {
                var resourceKey = key;

                // This is just for backward compatibility!
                if (!string.IsNullOrEmpty(dict) || !string.IsNullOrEmpty(assembly))
                    resourceKey = (dict ?? "") + ":" + resourceKey;
                if (!string.IsNullOrEmpty(assembly))
                    resourceKey = assembly + ":" + resourceKey;
                
                return resourceKey;
            }
            set
            {
                if (key != value)
                {
                    key = value;
                    UpdateNewValue();

                    OnNotifyPropertyChanged("Key");
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the Assembly where the .resx is located.
        /// </summary>
        [Obsolete("This property is obsolete and will be removed from the extension in the future.\r\nUse the attached properties of the ResxLocalizationProvider instead.")]
        public string Assembly
        {
            get { return this.assembly; }
            set { this.assembly = !string.IsNullOrEmpty(value) ? value : null; }
        }

        /// <summary>
        /// Gets or sets the name of the Dict where the .resx is located.
        /// </summary>
        [Obsolete("This property is obsolete and will be removed from the extension in the future.\r\nUse the attached properties of the ResxLocalizationProvider instead.")]
        public string Dict
        {
            get { return this.dict; }
            set { this.dict = !string.IsNullOrEmpty(value) ? value : null; }
        }

        /// <summary>
        /// Gets or sets the custom value converter.
        /// </summary>
        public IValueConverter Converter
        {
            get
            {
                if (converter == null)
                    converter = new DefaultConverter();

                return converter;
            }
            set { converter = value; }
        }

        /// <summary>
        /// Gets or sets the converter parameter.
        /// </summary>
        public object ConverterParameter
        {
            get { return converterParameter; }
            set { converterParameter = value; }
        }

        /// <summary>
        /// Gets or sets the culture to force a fixed localized object
        /// </summary>
        public string ForceCulture { get; set; }

        /// <summary>
        /// Gets or sets the initialize value.
        /// This is ONLY used to support the localize extension in blend!
        /// </summary>
        /// <value>The initialize value.</value>
        [EditorBrowsable(EditorBrowsableState.Never)]
#if SILVERLIGHT
#else
        [ConstructorArgument("key")]
#endif
        public string InitializeValue { get; set; }

        /// <summary>
        /// Gets or sets the Key that identifies a resource (Assembly:Dictionary:Key)
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ResourceIdentifierKey
        {
            get { return string.Format("{0}:{1}:{2}", assembly, dict, key ?? "(null)"); }
            set { ResxLocalizationProvider.ParseKey(value, out assembly, out dict, out key); }
        }
        #endregion

        #region Constructors & Dispose
        /// <summary>
        /// Initializes a new instance of the <see cref="LocExtension"/> class.
        /// </summary>
        public LocExtension()
            : base()
        {
            // Register this extension as an event listener on the first target.
            base.OnFirstTarget = () =>
            {
                LocalizeDictionary.DictionaryEvent.AddListener(this);
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocExtension"/> class.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        public LocExtension(string key)
            : this()
        {
            this.Key = key;
        }

        /// <summary>
        /// Removes the listener from the dictionary.
        /// <para>The "new" keyword is just a temporary hack in order to keep XAMLMarkupExtensions on the current version.</para>
        /// </summary>
        public new void Dispose()
        {
            base.Dispose();
            LocalizeDictionary.DictionaryEvent.RemoveListener(this);
        }

        /// <summary>
        /// The finalizer.
        /// </summary>
        ~LocExtension()
        {
            Dispose();
        }
        #endregion

        #region IDictionaryEventListener implementation
        /// <summary>
        /// This method is called when the resource somehow changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        public void ResourceChanged(DependencyObject sender, DictionaryEventArgs e)
        {
            ClearItemFromResourceBuffer(e);
            if (sender == null)
            {
                UpdateNewValue();
                return;
            }

            // Update, if this object is in our endpoint list.
            var targetDOs = (from p in GetTargetPropertyPaths()
                             select p.EndPoint.TargetObject as DependencyObject);

            foreach (var dObj in targetDOs)
            {
#if !SILVERLIGHT
                if (LocalizeDictionary.Instance.DefaultProvider is InheritingResxLocalizationProvider)
                {
                    UpdateNewValue();
                    break;
                }
#endif

                var doParent = dObj;
                while (doParent != null)
                {
                    if (sender == doParent)
                    {
                        UpdateNewValue();
                        break;
                    }
#if !SILVERLIGHT
                    if (!(doParent is Visual) && !(doParent is Visual3D))
                    {
                        UpdateNewValue();
                        break;
                    }
#endif
                    try
                    {
                        var doParent2 = VisualTreeHelper.GetParent(doParent);
                        if (doParent2 == null && doParent is FrameworkElement)
                            doParent2 = ((FrameworkElement)doParent).Parent;

                        doParent = doParent2;
                    }
                    catch
                    {
                        UpdateNewValue();
                        break;
                    }
                }
            }
        }

        private void ClearItemFromResourceBuffer(DictionaryEventArgs dictionaryEventArgs)
        {
            if (dictionaryEventArgs.Type == DictionaryEventType.ValueChanged)
            {
                var args = dictionaryEventArgs.Tag as ValueChangedEventArgs;
                if (args != null)
                {
                    var keysToRemove = new List<string>();
                    var ci = args.Tag as CultureInfo;
                    foreach (var cacheKey in ResourceBuffer.Keys)
                    {
                        if (cacheKey.EndsWith(args.Key))
                        {
                            if (ci == null || cacheKey.StartsWith(ci.Name))
                            {
                                if (ResourceBuffer[cacheKey] != args.Value)
                                {
                                    keysToRemove.Add(cacheKey);
                                }
                            }

                        }
                    }
                    foreach (var keyToRemove in keysToRemove)
                    {
                        if (!ResourceBuffer.ContainsKey(keyToRemove))
                        {
                            continue;
                        }
                        if (ResourceBuffer[keyToRemove] != args.Value)
                        {
                            ResourceBuffer.Remove(keyToRemove);
                        }
                    }
                }
            }
        }

        #endregion

        #region Forced culture handling
        /// <summary>
        /// If Culture property defines a valid <see cref="CultureInfo"/>, a <see cref="CultureInfo"/> instance will get
        /// created and returned, otherwise <see cref="LocalizeDictionary"/>.Culture will get returned.
        /// </summary>
        /// <returns>The <see cref="CultureInfo"/></returns>
        /// <exception cref="System.ArgumentException">
        /// thrown if the parameter Culture don't defines a valid <see cref="CultureInfo"/>
        /// </exception>
        protected CultureInfo GetForcedCultureOrDefault()
        {
            // define a culture info
            CultureInfo cultureInfo;

            // check if the forced culture is not null or empty
            if (!string.IsNullOrEmpty(this.ForceCulture))
            {
                // try to create a valid cultureinfo, if defined
                try
                {
                    // try to create a specific culture from the forced one
                    // cultureInfo = CultureInfo.CreateSpecificCulture(this.ForceCulture);
                    cultureInfo = new CultureInfo(this.ForceCulture);
                }
                catch (ArgumentException ex)
                {
                    // on error, check if designmode is on
                    if (LocalizeDictionary.Instance.GetIsInDesignMode())
                    {
                        // cultureInfo will be set to the current specific culture
#if SILVERLIGHT
                        cultureInfo = LocalizeDictionary.Instance.Culture;
#else
                        cultureInfo = LocalizeDictionary.Instance.SpecificCulture;
#endif
                    }
                    else
                    {
                        // tell the customer, that the forced culture cannot be converted propperly
                        throw new ArgumentException("Cannot create a CultureInfo with '" + this.ForceCulture + "'", ex);
                    }
                }
            }
            else
            {
                // take the current specific culture
#if SILVERLIGHT
                cultureInfo = LocalizeDictionary.Instance.Culture;
#else
                cultureInfo = LocalizeDictionary.Instance.SpecificCulture;
#endif
            }

            // return the evaluated culture info
            return cultureInfo;
        } 
        #endregion

        #region TargetMarkupExtension implementation
        /// <summary>
        /// This function returns the properly prepared output of the markup extension.
        /// </summary>
        /// <param name="info">Information about the target.</param>
        /// <param name="endPoint">Information about the endpoint.</param>
        public override object FormatOutput(TargetInfo endPoint, TargetInfo info)
        {
            object result = null;

            if (endPoint == null)
                return null;

            var targetObject = endPoint.TargetObject as DependencyObject;

            // Get target type. Change ImageSource to BitmapSource in order to use our own converter.
            Type targetType = info.TargetPropertyType;

            if (targetType.Equals(typeof(System.Windows.Media.ImageSource)))
                targetType = typeof(BitmapSource);

            // In case of a list target, get the correct list element type.
            if ((info.TargetPropertyIndex != -1) && typeof(IList).IsAssignableFrom(info.TargetPropertyType))
                targetType = info.TargetPropertyType.GetGenericArguments()[0];
            
            // Try to get the localized input from the resource.
            string resourceKey = this.Key;
            
            CultureInfo ci = GetForcedCultureOrDefault();

            // Extract the names of the endpoint object and property
            string epName = "";
            string epProp = "";

            if (endPoint.TargetObject is FrameworkElement)
                epName = (string)((FrameworkElement)endPoint.TargetObject).GetValue(FrameworkElement.NameProperty);
#if SILVERLIGHT
#else
            else if (endPoint.TargetObject is FrameworkContentElement)
                epName = (string)((FrameworkContentElement)endPoint.TargetObject).GetValue(FrameworkContentElement.NameProperty);
#endif

            if (endPoint.TargetProperty is PropertyInfo)
                epProp = ((PropertyInfo)endPoint.TargetProperty).Name;
#if SILVERLIGHT
            else if (endPoint.TargetProperty is DependencyProperty)
                epProp = ((DependencyProperty)endPoint.TargetProperty).ToString();
#else
            else if (endPoint.TargetProperty is DependencyProperty)
                epProp = ((DependencyProperty)endPoint.TargetProperty).Name;
#endif

            // What are these names during design time good for? Any suggestions?
            if (epProp.Contains("FrameworkElementWidth5"))
                epProp = "Height";
            else if (epProp.Contains("FrameworkElementWidth6"))
                epProp = "Width";
            else if (epProp.Contains("FrameworkElementMargin12"))
                epProp = "Margin";

            string resKeyBase = ci.Name + ":" + targetType.Name + ":";
            string resKeyNameProp = epName + LocalizeDictionary.GetSeparation(targetObject) + epProp;
            string resKeyName = epName;
            
            // Check, if the key is already in our resource buffer.
            object input = null;
            var isDefaultConverter = this.Converter is DefaultConverter;

            if (!String.IsNullOrEmpty(resourceKey))
            {
                // We've got a resource key. Try to look it up or get it from the dictionary.
                if (isDefaultConverter && ResourceBuffer.ContainsKey(resKeyBase + resourceKey))
                    result = ResourceBuffer[resKeyBase + resourceKey];
                else
                {
                    input = LocalizeDictionary.Instance.GetLocalizedObject(resourceKey, targetObject, ci);
                    resKeyBase += resourceKey;
                }
            }
            else
            {
                // Try the automatic lookup function.
                // First, look for a resource entry named: [FrameworkElement name][Separator][Property name]
                if (isDefaultConverter && ResourceBuffer.ContainsKey(resKeyBase + resKeyNameProp))
                    result = ResourceBuffer[resKeyBase + resKeyNameProp];
                else
                {
                    // It was not stored in the buffer - try to retrieve it from the dictionary.
                    input = LocalizeDictionary.Instance.GetLocalizedObject(resKeyNameProp, targetObject, ci);

                    if (input == null)
                    {
                        // Now, try to look for a resource entry named: [FrameworkElement name]
                        // Note - this has to be nested here, as it would take precedence over the first step in the buffer lookup step.
                        if (isDefaultConverter && ResourceBuffer.ContainsKey(resKeyBase + resKeyName))
                            result = ResourceBuffer[resKeyBase + resKeyName];
                        else
                        {
                            input = LocalizeDictionary.Instance.GetLocalizedObject(resKeyName, targetObject, ci);
                            resKeyBase += resKeyName;
                        }
                    }
                    else
                        resKeyBase += resKeyNameProp;
                }
            }

            // If no result was found, convert the input and add it to the buffer.
            if (result == null && input != null)
            {
                result = this.Converter.Convert(input, targetType, this.ConverterParameter, ci);
                if (isDefaultConverter)
                    ResourceBuffer.Add(resKeyBase, result);
            }

            return result;
        }

        /// <summary>
        /// This method must return true, if an update shall be executed when the given endpoint is reached.
        /// This method is called each time an endpoint is reached.
        /// </summary>
        /// <param name="endpoint">Information on the specific endpoint.</param>
        /// <returns>True, if an update of the path to this endpoint shall be performed.</returns>
        protected override bool UpdateOnEndpoint(TargetInfo endpoint)
        {
            // This extension must be updated, when an endpoint is reached.
            return true;
        }
        #endregion

        #region Resolve functions
        /// <summary>
        /// Resolves the localized value of the current Assembly, Dict, Key pair.
        /// </summary>
        /// <param name="resolvedValue">The resolved value.</param>
        /// <typeparam name="TValue">The type of the return value.</typeparam>
        /// <returns>
        /// True if the resolve was success, otherwise false.
        /// </returns>
        /// <exception>
        /// If the Assembly, Dict, Key pair was not found.
        /// </exception>
        public bool ResolveLocalizedValue<TValue>(out TValue resolvedValue)
        {
            // return the resolved localized value with the current or forced culture.
            return this.ResolveLocalizedValue(out resolvedValue, this.GetForcedCultureOrDefault(), null);
        }

        /// <summary>
        /// Resolves the localized value of the current Assembly, Dict, Key pair and the given target.
        /// </summary>
        /// <param name="resolvedValue">The resolved value.</param>
        /// <typeparam name="TValue">The type of the return value.</typeparam>
        /// <param name="target">The target object.</param>
        /// <returns>
        /// True if the resolve was success, otherwise false.
        /// </returns>
        /// <exception>
        /// If the Assembly, Dict, Key pair was not found.
        /// </exception>
        public bool ResolveLocalizedValue<TValue>(out TValue resolvedValue, DependencyObject target)
        {
            // return the resolved localized value with the current or forced culture.
            return this.ResolveLocalizedValue(out resolvedValue, this.GetForcedCultureOrDefault(), target);
        }

        /// <summary>
        /// Resolves the localized value of the current Assembly, Dict, Key pair.
        /// </summary>
        /// <param name="resolvedValue">The resolved value.</param>
        /// <param name="targetCulture">The target culture.</param>
        /// <typeparam name="TValue">The type of the return value.</typeparam>
        /// <returns>
        /// True if the resolve was success, otherwise false.
        /// </returns>
        /// <exception>
        /// If the Assembly, Dict, Key pair was not found.
        /// </exception>
        public bool ResolveLocalizedValue<TValue>(out TValue resolvedValue, CultureInfo targetCulture)
        {
            return ResolveLocalizedValue(out resolvedValue, targetCulture, null);
        }

        /// <summary>
        /// Resolves the localized value of the current Assembly, Dict, Key pair and the given target.
        /// </summary>
        /// <param name="resolvedValue">The resolved value.</param>
        /// <param name="targetCulture">The target culture.</param>
        /// <param name="target">The target object.</param>
        /// <typeparam name="TValue">The type of the return value.</typeparam>
        /// <returns>
        /// True if the resolve was success, otherwise false.
        /// </returns>
        /// <exception>
        /// If the Assembly, Dict, Key pair was not found.
        /// </exception>
        public bool ResolveLocalizedValue<TValue>(out TValue resolvedValue, CultureInfo targetCulture, DependencyObject target)
        {
            // define the default value of the resolved value
            resolvedValue = default(TValue);

            // get the localized object from the dictionary
            string resKey = targetCulture.Name + ":" + typeof(TValue).Name + ":" + this.Key;
            var isDefaultConverter = this.Converter is DefaultConverter;

            if (isDefaultConverter && ResourceBuffer.ContainsKey(resKey))
            {
                resolvedValue = (TValue)ResourceBuffer[resKey];
            }
            else
            {
                object localizedObject = LocalizeDictionary.Instance.GetLocalizedObject(this.Key, target, targetCulture);

                if (localizedObject == null)
                    return false;

                object result = this.Converter.Convert(localizedObject, typeof(TValue), this.ConverterParameter, targetCulture);
                
                if (result is TValue)
                {
                    resolvedValue = (TValue)result;
                    if (isDefaultConverter)
                        ResourceBuffer.Add(resKey, resolvedValue);
                }
            }

            if (resolvedValue != null)
                return true;

            return false;
        }
        #endregion

        #region Code-behind binding
        /// <summary>
        /// Sets a binding between a <see cref="DependencyObject"/> with its <see cref="DependencyProperty"/>
        /// or <see cref="PropertyInfo"/> and the <c>LocExtension</c>.
        /// </summary>
        /// <param name="targetObject">The target dependency object</param>
        /// <param name="targetProperty">The target property</param>
        /// <returns>
        /// TRUE if the binding was setup successfully, otherwise FALSE (Binding already exists).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If the <paramref name="targetProperty"/> is
        /// not a <see cref="DependencyProperty"/> or <see cref="PropertyInfo"/>.
        /// </exception>
        public bool SetBinding(DependencyObject targetObject, object targetProperty)
        {
            return SetBinding((object)targetObject, targetProperty, -1);
        }

        /// <summary>
        /// Sets a binding between a <see cref="DependencyObject"/> with its <see cref="DependencyProperty"/>
        /// or <see cref="PropertyInfo"/> and the <c>LocExtension</c>.
        /// </summary>
        /// <param name="targetObject">The target object</param>
        /// <param name="targetProperty">The target property</param>
        /// <returns>
        /// TRUE if the binding was setup successfully, otherwise FALSE (Binding already exists).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If the <paramref name="targetProperty"/> is
        /// not a <see cref="DependencyProperty"/> or <see cref="PropertyInfo"/>.
        /// </exception>
        public bool SetBinding(object targetObject, object targetProperty)
        {
            return SetBinding((object)targetObject, targetProperty, -1);
        }

        /// <summary>
        /// Sets a binding between a <see cref="DependencyObject"/> with its <see cref="DependencyProperty"/>
        /// or <see cref="PropertyInfo"/> and the <c>LocExtension</c>.
        /// </summary>
        /// <param name="targetObject">The target dependency object</param>
        /// <param name="targetProperty">The target property</param>
        /// <param name="targetPropertyIndex">The index of the target property. (only used for Lists)</param>
        /// <returns>
        /// TRUE if the binding was setup successfully, otherwise FALSE (Binding already exists).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If the <paramref name="targetProperty"/> is
        /// not a <see cref="DependencyProperty"/> or <see cref="PropertyInfo"/>.
        /// </exception>
        public bool SetBinding(DependencyObject targetObject, object targetProperty, int targetPropertyIndex)
        {
            return SetBinding((object)targetObject, targetProperty, targetPropertyIndex);
        }

        /// <summary>
        /// Sets a binding between a <see cref="DependencyObject"/> with its <see cref="DependencyProperty"/>
        /// or <see cref="PropertyInfo"/> and the <c>LocExtension</c>.
        /// </summary>
        /// <param name="targetObject">The target object</param>
        /// <param name="targetProperty">The target property</param>
        /// <param name="targetPropertyIndex">The index of the target property. (only used for Lists)</param>
        /// <returns>
        /// TRUE if the binding was setup successfully, otherwise FALSE (Binding already exists).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If the <paramref name="targetProperty"/> is
        /// not a <see cref="DependencyProperty"/> or <see cref="PropertyInfo"/>.
        /// </exception>
        public bool SetBinding(object targetObject, object targetProperty, int targetPropertyIndex)
        {
            var existingBinding = (from info in GetTargetPropertyPaths()
                                   where (info.EndPoint.TargetObject == targetObject) && (info.EndPoint.TargetProperty == targetProperty)
                                   select info).FirstOrDefault();

            // Return false, if the binding already exists
            if (existingBinding != null)
                return false;

            Type targetPropertyType = null;

            if (targetProperty is DependencyProperty)
#if SILVERLIGHT
                // Dirty reflection hack - get the property type (property not included in the SL DependencyProperty class) from the internal declared field.
                targetPropertyType = typeof(DependencyProperty).GetField("_propertyType", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(targetProperty) as Type;
#else
                targetPropertyType = ((DependencyProperty)targetProperty).PropertyType;
#endif
            else if (targetProperty is PropertyInfo)
                targetPropertyType = ((PropertyInfo)targetProperty).PropertyType;

            var result = ProvideValue(new SimpleProvideValueServiceProvider(targetObject, targetProperty, targetPropertyType, targetPropertyIndex));

            SetPropertyValue(result, new TargetInfo(targetObject, targetProperty, targetPropertyType, targetPropertyIndex), false);

            return true;
        }
        #endregion

        /// <summary>
        /// Overridden, to return the key of this instance.
        /// </summary>
        /// <returns>Loc: + key</returns>
        public override string ToString()
        {
            return "Loc:" + key;
        }
    }
}