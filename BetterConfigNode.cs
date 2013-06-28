/*
 * Created by SharpDevelop.
 * User: Bernhard
 * Date: 28.06.2013
 * Time: 16:51
 */

using UnityEngine;
using System;

/// <summary>
/// Description of BetterConfigNode.
/// </summary>
public static class BetterConfigNode
{
	public static string GetValueDefault(this ConfigNode config, string name, string default_value) {
		string val = config.GetValue(name);
		return (val != null ? val : default_value);
	}
	public static float GetValueDefault(this ConfigNode config, string name, float default_value) {
		string val = config.GetValue(name);
		return (val != null ? float.Parse(val) : default_value);
	}
	public static double GetValueDefault(this ConfigNode config, string name, double default_value) {
		string val = config.GetValue(name);
		return (val != null ? double.Parse(val) : default_value);
	}
	public static bool GetValueDefault(this ConfigNode config, string name, bool default_value) {
		string val = config.GetValue(name);
		return (val != null ? bool.Parse(val) : default_value);
	}
	public static Vector2 GetValueDefault(this ConfigNode config, string name, Vector2 default_value) {
		string val = config.GetValue(name);
		if (val != null) { return (Vector2)ConfigNode.ParseVector2(val.Replace("(","").Replace(")","")); } // why does ParseVector2 return Vector3? also it can't handle the ( )
		return (default_value);
	}
	public static Vector3 GetValueDefault(this ConfigNode config, string name, Vector3 default_value) {
		string val = config.GetValue(name);
		if (val != null) { return ConfigNode.ParseVector3(val.Replace("(","").Replace(")","")); }
		return (default_value);
	}
	public static Vector3d GetValueDefault(this ConfigNode config, string name, Vector3d default_value) {
		string val = config.GetValue(name);
		if (val != null) { return ConfigNode.ParseVector3D(val.Replace("(","").Replace(")","")); }
		return (default_value);
	}
	public static Vector4 GetValueDefault(this ConfigNode config, string name, Vector4 default_value) {
		string val = config.GetValue(name);
		if (val != null) { return ConfigNode.ParseVector4(val.Replace("(","").Replace(")","")); }
		return (default_value);
	}
	public static Color GetValueDefault(this ConfigNode config, string name, Color default_value) {
		string val = config.GetValue(name);
		return (val != null ? ConfigNode.ParseColor(val) : default_value);
	}
	public static Color32 GetValueDefault(this ConfigNode config, string name, Color32 default_value) {
		string val = config.GetValue(name);
		return (val != null ? ConfigNode.ParseColor32(val) : default_value);
	}
	public static Quaternion GetValueDefault(this ConfigNode config, string name, Quaternion default_value) {
		string val = config.GetValue(name);
		return (val != null ? ConfigNode.ParseQuaternion(val) : default_value);
	}
	public static QuaternionD GetValueDefault(this ConfigNode config, string name, QuaternionD default_value) {
		string val = config.GetValue(name);
		return (val != null ? ConfigNode.ParseQuaternionD(val) : default_value);
	}
	public static Enum GetValueDefault(this ConfigNode config, Type enumType, string name, Enum default_value) {
		string val = config.GetValue(name);
		return (val != null ? ConfigNode.ParseEnum(enumType, val) : default_value);
	}
}
