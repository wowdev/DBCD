using DBDefsLib;
using DBFileReaderLib;
using DBFileReaderLib.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace DBCD
{

    public struct DBCDInfo
    {
        internal string tableName;

        internal string[] availableColumns;
    }

    internal class DBCDBuilder
    {
        private ModuleBuilder moduleBuilder;
        private int locStringSize;

        internal DBCDBuilder()
        {
            var assemblyName = new AssemblyName("DBCDDefinitons");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            this.moduleBuilder = moduleBuilder;
            this.locStringSize = 1;
        }

        internal Tuple<Type, DBCDInfo> Build(DBReader dbcReader, Stream dbd, string name = null, string build = null)
        {
            var dbdReader = new DBDReader();

            if (name == null)
            {
                name = Guid.NewGuid().ToString();
            }

            var databaseDefinition = dbdReader.Read(dbd);

            Structs.VersionDefinitions? versionDefinition = null;

            if (dbcReader.LayoutHash != 0)
            {
                var layoutHash = dbcReader.LayoutHash.ToString("X8");
                Utils.GetVersionDefinitionByLayoutHash(databaseDefinition, layoutHash, out versionDefinition);
            }

            if (versionDefinition == null && !string.IsNullOrWhiteSpace(build))
            {
                var dbBuild = new Build(build);
                locStringSize = GetLocStringSize(dbBuild);
                Utils.GetVersionDefinitionByBuild(databaseDefinition, dbBuild, out versionDefinition);
            }            

            if (versionDefinition == null)
            {
                throw new FileNotFoundException("No definition found for this file.");
            }

            var typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public);

            var fields = versionDefinition.Value.definitions;
            var columns = new List<string>(fields.Length);

            foreach (var fieldDefinition in fields)
            {
                var columnDefinition = databaseDefinition.columnDefinitions[fieldDefinition.name];
                bool isLocalisedString = columnDefinition.type == "locstring" && locStringSize > 1;

                var fieldType = FieldDefinitionToType(fieldDefinition, columnDefinition);
                var field = typeBuilder.DefineField(fieldDefinition.name, fieldType, FieldAttributes.Public);

                columns.Add(fieldDefinition.name);

                if (fieldDefinition.isID)
                {
                    AddAttribute<IndexAttribute>(field);
                }

                if (fieldDefinition.arrLength > 1)
                {
                    AddAttribute<CardinalityAttribute>(field, fieldDefinition.arrLength);
                }

                if (isLocalisedString)
                {
                    AddAttribute<CardinalityAttribute>(field, locStringSize);

                    typeBuilder.DefineField(fieldDefinition.name + "_mask", typeof(uint), FieldAttributes.Public);
                    columns.Add(fieldDefinition.name + "_mask");
                }
            }

            var type = typeBuilder.CreateTypeInfo();

            var info = new DBCDInfo
            {
                availableColumns = columns.ToArray(),
                tableName = name
            };

            return new Tuple<Type, DBCDInfo>(type, info);
        }

        private int GetLocStringSize(Build build)
        {
            // post wotlk
            if (build.expansion >= 4 || build.build > 12340)
                return 1;

            // tbc - wotlk
            if (build.build >= 6692)
                return 16;

            // alpha - vanilla
            return 8;
        }

        private void AddAttribute<T>(FieldBuilder field, params object[] parameters) where T : Attribute
        {
            var constructorParameters = parameters.Select(x => x.GetType()).ToArray();
            var constructorInfo = typeof(T).GetConstructor(constructorParameters);
            var attributeBuilder = new CustomAttributeBuilder(constructorInfo, parameters);
            field.SetCustomAttribute(attributeBuilder);
        }

        private Type FieldDefinitionToType(Structs.Definition field, Structs.ColumnDefinition column)
        {
            var isArray = field.arrLength != 0;

            switch (column.type)
            {
                case "int":
                    {
                        Type type = null;
                        var signed = field.isSigned;

                        switch (field.size)
                        {
                            case 8:
                                type = signed ? typeof(sbyte) : typeof(byte);
                                break;
                            case 16:
                                type = signed ? typeof(short) : typeof(ushort);
                                break;
                            case 32:
                                type = signed ? typeof(int) : typeof(uint);
                                break;
                            case 64:
                                type = signed ? typeof(long) : typeof(ulong);
                                break;
                        }

                        return isArray ? type.MakeArrayType() : type;
                    }
                case "string":
                    return isArray ? typeof(string[]) : typeof(string);
                case "locstring":
                    return locStringSize > 1 || isArray ? typeof(string[]) : typeof(string);
                case "float":
                    return isArray ? typeof(float[]) : typeof(float);
                default:
                    throw new ArgumentException("Unable to construct C# type from " + column.type);
            }
        }
    }
}