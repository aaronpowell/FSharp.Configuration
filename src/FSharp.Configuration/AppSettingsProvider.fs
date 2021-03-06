module FSharp.Configuration.AppSettingsTypeProvider

open FSharp.Configuration.Helper
open Samples.FSharp.ProvidedTypes
open System
open System.Configuration
open System.Collections.Generic
open System.Globalization

let getConfigValue(key) =
    let settings = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings
    settings.[key].Value

let setConfigValue(key, value) = 
    let settings = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None)
    settings.AppSettings.Settings.[key].Value <- value
    settings.Save()

let internal typedAppSettings (context: Context) =
    let appSettings = erasedType<obj> thisAssembly rootNamespace "AppSettings"

    appSettings.DefineStaticParameters(
        parameters = [ProvidedStaticParameter("configFileName", typeof<string>)], 
        instantiationFunction = (fun typeName parameterValues ->
            match parameterValues with 
            | [| :? string as configFileName |] ->
                let typeDef = erasedType<obj> thisAssembly rootNamespace typeName
                let names = HashSet()
                try
                    let filePath = findConfigFile context.ResolutionFolder configFileName
                    let fileMap = ExeConfigurationFileMap(ExeConfigFilename=filePath)
                    let appSettings = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None).AppSettings.Settings

                    for key in appSettings.AllKeys do
                        let name = niceName names key
                        let prop =
                            match (appSettings.Item key).Value with
                            | ValueParser.Uri _ ->
                                ProvidedProperty(name, typeof<int>,
                                    GetterCode = (fun _ -> <@@ Uri (getConfigValue key) @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, string ((%%args.[0]):Uri)) @@>)
                            | ValueParser.Int _ ->
                                ProvidedProperty(name, typeof<int>,
                                    GetterCode = (fun _ -> <@@ Int32.Parse (getConfigValue key) @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, string ((%%args.[0]):Int32)) @@>)
                            | ValueParser.Bool _ ->
                                ProvidedProperty(name, typeof<bool>,
                                    GetterCode = (fun _ -> <@@ Boolean.Parse (getConfigValue key) @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, string ((%%args.[0]):Boolean)) @@>)                                                                                    
                            | ValueParser.Float _ ->
                                ProvidedProperty(name, typeof<float>,
                                    GetterCode = (fun _ -> <@@ Double.Parse (getConfigValue key, NumberStyles.Any, CultureInfo.InvariantCulture) @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, string ((%%args.[0]):float)) @@>)
                            | ValueParser.TimeSpan _ ->
                                ProvidedProperty(name, typeof<TimeSpan>,
                                    GetterCode = (fun _ -> <@@ TimeSpan.Parse(getConfigValue key, CultureInfo.InvariantCulture) @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, string ((%%args.[0]):TimeSpan)) @@>)
                            | ValueParser.DateTime _ ->
                                ProvidedProperty(name, typeof<DateTime>,
                                    GetterCode = (fun _ -> <@@ DateTime.Parse(getConfigValue key, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, ((%%args.[0]):DateTime).ToString("o")) @@>)
                            | _ ->
                                ProvidedProperty(name, typeof<string>,
                                    GetterCode = (fun _ -> <@@ getConfigValue key @@>),
                                    SetterCode = fun args -> <@@ setConfigValue(key, %%args.[0]) @@>)

                        prop.IsStatic <- true
                        prop.AddXmlDoc (sprintf "Returns the value from %s with key %s" configFileName key)
                        prop.AddDefinitionLocation(1,1,filePath)

                        typeDef.AddMember prop

                    let name = niceName names "ConfigFileName"
                    let getValue = <@@ filePath @@>
                    let prop = ProvidedProperty(name, typeof<string>, GetterCode = (fun _ -> getValue))

                    prop.IsStatic <- true
                    prop.AddXmlDoc "Returns the Filename"

                    typeDef.AddMember prop
                    context.WatchFile filePath
                    typeDef
                with _ -> typeDef
            | x -> failwithf "unexpected parameter values %A" x))
    appSettings