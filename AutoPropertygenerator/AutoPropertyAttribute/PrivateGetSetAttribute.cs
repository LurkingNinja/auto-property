﻿using System;

namespace AutoPropertyAttribute
{
    [AttributeUsage(AttributeTargets.Field)]
    public class PrivateGetSet : Attribute {}
}