﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Common;
public interface IXmlInit { }
/// <summary>
/// Reads from XML attribute
/// </summary>
public interface IAtt : IXmlInit {
    /// <summary>
    /// Split the value according to <c>separator</c> before parsing
    /// </summary>
    public string separator { get; }
    /// <summary>
    /// Automatically parse the value and pass the result into <c>convert</c>. Otherwise, pass the raw value into <c>convert</c>
    /// </summary>
    public bool parse { get; }
    /// <summary>
    /// Look for subelement named by <c>alias</c> instead of the field name. Corresponding <c>convert</c> will be called under the field name
    /// </summary>
    public string alias { get; }
}
/// <summary>
/// The XML element must have an attribute named by this field.
/// </summary>
public class Req : Attribute, IAtt {
    public string separator { get; set; } = null;
    public bool parse { get; set; } = true;
    public string alias { get; set; } = null;
}
/// <summary>
/// The XML element may have an attribute named by this field.
/// </summary>
public class Opt : Attribute, IAtt {
    public string separator { get; set; } = null;
    public bool parse { get; set; } = true;
    public string alias { get; set; } = null;
}
/// <summary>
/// Reads from XML element
/// </summary>
public interface IEle : IXmlInit {
    /// <summary>
    /// Auto construct the object from <c>XElement</c> using reflection before passing into <c>convert</c>. Otherwise, <c>convert</c> receives the raw <c>XElement</c>
    /// </summary>
    public bool construct { get; set; }
    /// <summary>
    /// Skip reading the <c>XElement</c> if the variable is not null.
    /// </summary>
    public bool fallback { get; set; }
}
/// <summary>
/// Reads from the XML element used in initialization.
/// </summary>
public class Self : Attribute, IEle {
    public bool construct { get; set; } = true;
    
    public bool fallback { get; set; } = false;
}
/// <summary>
/// Read from a subelement named by the variable.
/// </summary>
public class Sub : Attribute, IEle {
    public bool construct { set; get; } = true;
    public bool fallback { get; set; } = false;
    /// <summary>
    /// The XML element has at least one of this subelement.
    /// </summary>
    public bool required = false;
    /// <summary>
    /// The XML element may have more than one of this subelement.
    /// </summary>
    public bool multiple = false;
    /// <summary>
    /// Look for subelement named by <c>alias</c> instead of the variable name. The corresponding <c>convert</c> will be called under the original var name
    /// </summary>
    public string alias { get; set; }
}