﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace Nez.Persistence
{
	/// <summary>
	/// responsible for caching as much of the reflection calls as we can. Should be cleared after each encode/decode.
	/// </summary>
	class CacheResolver
	{
		Dictionary<string, object> _referenceTracker = new Dictionary<string, object>();
		Dictionary<Type, ConstructorInfo> _constructorCache = new Dictionary<Type, ConstructorInfo>();
		Dictionary<Type, Dictionary<string, FieldInfo>> _fieldInfoCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
		Dictionary<Type, Dictionary<string, PropertyInfo>> _propertyInfoCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
		Dictionary<MemberInfo, bool> _memberInfoEncodeableCache = new Dictionary<MemberInfo, bool>();

		/// <summary>
		/// checks the <paramref name="memberInfo"/> custom attributes to see if it should be encoded/decoded
		/// </summary>
		/// <returns><c>true</c>, if member info encodeable or decodeable was ised, <c>false</c> otherwise.</returns>
		/// <param name="memberInfo">Member info.</param>
		/// <param name="isPublic">If set to <c>true</c> is public.</param>
		internal bool IsMemberInfoEncodeableOrDecodeable( MemberInfo memberInfo, bool isPublic )
		{
			if( _memberInfoEncodeableCache.TryGetValue( memberInfo, out var isEncodeable ) )
				return isEncodeable;
				
			foreach( var attribute in memberInfo.GetCustomAttributes( true ) )
			{
				if( Json.excludeAttrType.IsInstanceOfType( attribute ) )
				{
					isPublic = false;
				}

				if( Json.includeAttrType.IsInstanceOfType( attribute ) )
				{
					isPublic = true;
				}
			}

			_memberInfoEncodeableCache[memberInfo] = isPublic;

			return isPublic;
		}

		internal void Clear()
		{
			_referenceTracker.Clear();
			_constructorCache.Clear();
			_fieldInfoCache.Clear();
			_propertyInfoCache.Clear();
			_memberInfoEncodeableCache.Clear();
		}

		internal void TrackReference<T>( string id, T instance ) => _referenceTracker[id] = instance;

		internal object GetReference( string refId ) => _referenceTracker[refId];

		/// <summary>
		/// Creates an instance of <paramref name="type"/> and caches the ConstructorInfo for future use
		/// </summary>
		/// <returns>The instance.</returns>
		/// <param name="type">Type.</param>
		internal object CreateInstance( Type type )
		{
			// structs have no constructors present so just let Activator.CreateInstance make them
			if( type.IsValueType )
				return Activator.CreateInstance( type );

			if( _constructorCache.TryGetValue( type, out var constructor ) )
				return constructor.Invoke( null );

			constructor = type.GetConstructor( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null );
			_constructorCache[type] = constructor;
			return constructor.Invoke( null );
		}


		#region FieldInfo methods

		internal IEnumerable<FieldInfo> GetEncodableFieldsForType( Type type, bool enforceHeirarchyOrderEnabled )
		{
			// cleanse the fields based on our attributes
			foreach( var kvPair in GetFieldInfoCache( type, enforceHeirarchyOrderEnabled ) )
			{
				if( IsMemberInfoEncodeableOrDecodeable( kvPair.Value, kvPair.Value.IsPublic ) )
					yield return kvPair.Value;
			}
		}

		/// <summary>
		/// Gets the FieldInfo with <paramref name="name"/> or if that isnt found checks for any matching
		/// <seealso cref="DecodeAliasAttribute"/>
		/// </summary>
		/// <returns>The field.</returns>
		/// <param name="type">Type.</param>
		/// <param name="name">Name.</param>
		internal FieldInfo GetField( Type type, string name )
		{
			var map = GetFieldInfoCache( type );
			if( map.TryGetValue( name, out var fieldInfo ) )
			{
				return fieldInfo;
			}

			// last resort: check DecodeAliasAttributes
			return FindFieldFromDecodeAlias( type, name );
		}

		Dictionary<string, FieldInfo> GetFieldInfoCache( Type type, bool enforceHeirarchyOrderEnabled = false )
		{
			if( _fieldInfoCache.TryGetValue( type, out var map ) )
			{
				return map;
			}

			// no data cached. Fetch and populate it now
			map = new Dictionary<string, FieldInfo>();
			_fieldInfoCache[type] = map;

			IEnumerable<FieldInfo> allFields = null;
			if( enforceHeirarchyOrderEnabled )
			{
				var types = new Stack<Type>();
				while( type != null )
				{
					types.Push( type );
					type = type.BaseType;
				}

				var fields = new List<FieldInfo>();
				while( types.Count > 0 )
				{
					fields.AddRange( types.Pop().GetFields( VariantConverter.instanceBindingFlags ) );
				}

				allFields = fields;
			}
			else
			{
				allFields = type.GetFields( VariantConverter.instanceBindingFlags );
			}

			// cleanse the fields based on our attributes
			foreach( var field in allFields )
			{
				map[field.Name] = field;
			}

			return map;
		}

		FieldInfo FindFieldFromDecodeAlias( Type type, string name )
		{
			foreach( var kvPair in _fieldInfoCache[type] )
			{
				foreach( var attribute in kvPair.Value.GetCustomAttributes( true ) )
				{
					if( VariantConverter.decodeAliasAttrType.IsInstanceOfType( attribute ) )
					{
						if( ( (DecodeAliasAttribute)attribute ).Contains( name ) )
						{
							return kvPair.Value;
						}
					}
				}
			}
			return null;
		}

		#endregion


		#region PropertyInfo methods

		internal IEnumerable<PropertyInfo> GetEncodablePropertiesForType( Type type, bool enforceHeirarchyOrderEnabled )
		{
			// cleanse the fields based on our attributes
			foreach( var kvPair in GetPropertyInfoCache( type, enforceHeirarchyOrderEnabled ) )
			{
				if( IsMemberInfoEncodeableOrDecodeable( kvPair.Value, true ) )
				{
					yield return kvPair.Value;
				}
			}
		}

		internal PropertyInfo GetEncodeableProperty( Type type, string name )
		{
			var map = GetPropertyInfoCache( type );
			if( map.TryGetValue( name, out var propInfo ) )
			{
				return propInfo;
			}

			// last resort: check DecodeAliasAttributes
			return FindPropertyFromDecodeAlias( type, name );
		}

		Dictionary<string, PropertyInfo> GetPropertyInfoCache( Type type, bool enforceHeirarchyOrderEnabled = false )
		{
			if( _propertyInfoCache.TryGetValue( type, out var map ) )
			{
				return map;
			}

			// no data cached. Fetch and populate it now
			map = new Dictionary<string, PropertyInfo>();
			_propertyInfoCache[type] = map;

			IEnumerable<PropertyInfo> allProps = null;
			if( enforceHeirarchyOrderEnabled )
			{
				var types = new Stack<Type>();
				while( type != null )
				{
					types.Push( type );
					type = type.BaseType;
				}

				var fields = new List<PropertyInfo>();
				while( types.Count > 0 )
				{
					fields.AddRange( types.Pop().GetProperties( BindingFlags.DeclaredOnly | VariantConverter.instanceBindingFlags ) );
				}

				allProps = fields;
			}
			else
			{
				allProps = type.GetProperties( VariantConverter.instanceBindingFlags );
			}

			// cleanse the fields based on our attributes
			foreach( var prop in allProps )
			{
				map[prop.Name] = prop;
			}

			return map;
		}

		PropertyInfo FindPropertyFromDecodeAlias( Type type, string name )
		{
			foreach( var kvPair in _propertyInfoCache[type] )
			{
				foreach( var attribute in kvPair.Value.GetCustomAttributes( true ) )
				{
					if( VariantConverter.decodeAliasAttrType.IsInstanceOfType( attribute ) )
					{
						if( ( (DecodeAliasAttribute)attribute ).Contains( name ) )
						{
							return kvPair.Value;
						}
					}
				}
			}
			return null;
		}

		#endregion

	}
}
