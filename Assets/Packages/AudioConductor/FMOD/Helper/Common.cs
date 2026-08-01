namespace ThanhDV.AudioConductor.FMOD
{
    public static class Common
    {
        public const string FMOD_DATA_FOLDER = "Assets/Plugins/AudioConductor/FMOD/Data";
        public const string FMOD_REF_SCRIPT_FOLDER = "Assets/Plugins/AudioConductor/FMOD/Scripts";
        public const string FMOD_PLATFORM_SCRIPT_FOLDER = "Assets/Plugins/AudioConductor/FMOD/Platform";

        public const string FMOD_BUS_SCRIPT_NAME = "FMODBus";
        public const string FMOD_EVENT_REF_SCRIPT_NAME = "FMODEventReference";
        public const string SCRIPT_EXTENSION = ".cs";
        public const string FMOD_BUS_SCRIPT_PATH = FMOD_REF_SCRIPT_FOLDER + "/" + FMOD_BUS_SCRIPT_NAME + SCRIPT_EXTENSION;
        public const string FMOD_EVENT_REF_SCRIPT_PATH = FMOD_REF_SCRIPT_FOLDER + "/" + FMOD_EVENT_REF_SCRIPT_NAME + SCRIPT_EXTENSION;

        public const string FMOD_JSON_NAME = "FMODReferences";
        public const string JSON_EXTENSION = ".json";
        public const string FMOD_JSON_PATH = FMOD_DATA_FOLDER + "/" + FMOD_JSON_NAME + JSON_EXTENSION;

        // Generated runtime assembly (holds FMODBus / FMODEventReference) so package editor tools can reference it by name.
        public const string GENERATED_ASMDEF_NAME = "AudioConductor.FMOD.Reference";
        public const string GENERATED_ASMDEF_PATH = FMOD_REF_SCRIPT_FOLDER + "/" + GENERATED_ASMDEF_NAME + ".asmdef";
        public const string MENU_ITEM = "Tools/ThanhDV/Audio Conductor - FMOD/";
    }
}
