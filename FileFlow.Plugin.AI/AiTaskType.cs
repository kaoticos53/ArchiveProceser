namespace FileFlow.Plugin.AI;

/// <summary>
/// Taxonomía de tareas y capacidades de Inteligencia Artificial en FileFlow Studio.
/// Permite generalizar los nodos para admitir diferentes modelos según la capacidad de la máquina.
/// </summary>
public enum AiTaskType
{
    /// <summary>
    /// Detección y localización de objetos en imágenes (TinyYOLO, YOLOv8, YOLO-World, etc.).
    /// </summary>
    ObjectDetection,

    /// <summary>
    /// Detección y localización de rostros humanos (UltraFace, RetinaFace, etc.).
    /// </summary>
    FaceDetection,

    /// <summary>
    /// Clasificación visual de categorías de imagen (MobileNet, ResNet, etc.).
    /// </summary>
    ImageClassification,

    /// <summary>
    /// Transcripción de voz y audio a texto y subtítulos (Whisper Tiny, Base, Small, Medium).
    /// </summary>
    SpeechToText,

    /// <summary>
    /// Traducción automática de texto entre idiomas (MarianMT, NLLB-200, etc.).
    /// </summary>
    TextTranslation,

    /// <summary>
    /// Modelos de lenguaje grandes locales para resúmenes y extracción (Qwen 2.5, Phi-3.5, etc.).
    /// </summary>
    TextGenerationLlm,

    /// <summary>
    /// Reconocimiento óptico de caracteres impresos en imágenes y documentos (Tesseract, etc.).
    /// </summary>
    Ocr,

    /// <summary>
    /// Segmentación de sujeto y eliminación de fondos en imágenes (RMBG, MODNet, etc.).
    /// </summary>
    BackgroundRemoval,

    /// <summary>
    /// Super-resolución y restauración neural de imágenes y documentos (Real-ESRGAN, etc.).
    /// </summary>
    SuperResolution,

    /// <summary>
    /// Moderación y detección de contenido sensible o inapropiado (OpenNSFW2, etc.).
    /// </summary>
    ContentModeration,

    /// <summary>
    /// Detección de actividad de voz y eliminación de silencios en audio (Silero VAD, etc.).
    /// </summary>
    VoiceActivityDetection,

    /// <summary>
    /// Síntesis de voz natural a partir de texto (Piper TTS, etc.).
    /// </summary>
    TextToSpeech,

    /// <summary>
    /// Detección y ofuscación de datos personales sensibles bajo RGPD/PII (Presidio, NER ONNX).
    /// </summary>
    PiiAnonymization,

    /// <summary>
    /// Codificación de embeddings vectoriales y búsqueda semántica multimodal (CLIP, BGE, MiniLM).
    /// </summary>
    SemanticEmbeddings
}
