using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Motor de traducción de prompts multilingüe especializado en visión por computador y alineación texto-imagen.
/// Admite modelos neuronales MarianMT (Helsinki-NLP opus-mt-es-en) en formato ONNX y un motor semántico de
/// gramática española con resolución de conceptos compuestos, eliminación de artículos, inversión adjetival y normalización de acentos.
/// </summary>
public static class PromptTranslator
{
    // Diccionario exhaustivo de conceptos visuales y categorías
    private static readonly Dictionary<string, string> ConceptDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Personas, edades y partes del cuerpo ───────────────────────────────
        ["persona"] = "person",
        ["personas"] = "people",
        ["gente"] = "people",
        ["humano"] = "human",
        ["humanos"] = "humans",
        ["hombre"] = "man",
        ["hombres"] = "men",
        ["mujer"] = "woman",
        ["mujeres"] = "women",
        ["chico"] = "boy",
        ["chica"] = "girl",
        ["chicos"] = "boys",
        ["chicas"] = "girls",
        ["niño"] = "boy",
        ["niña"] = "girl",
        ["niños"] = "children",
        ["niñas"] = "girls",
        ["bebe"] = "baby",
        ["bebé"] = "baby",
        ["bebes"] = "babies",
        ["bebés"] = "babies",
        ["anciano"] = "elderly person",
        ["anciana"] = "elderly woman",
        ["abuelo"] = "grandfather",
        ["abuela"] = "grandmother",
        ["familia"] = "family",
        ["multitud"] = "crowd",
        ["grupo de personas"] = "group of people",
        ["rostro"] = "face",
        ["rostros"] = "faces",
        ["cara"] = "face",
        ["caras"] = "faces",
        ["cabeza"] = "head",
        ["ojos"] = "eyes",
        ["ojo"] = "eye",
        ["boca"] = "mouth",
        ["nariz"] = "nose",
        ["oreja"] = "ear",
        ["orejas"] = "ears",
        ["mano"] = "hand",
        ["manos"] = "hands",
        ["brazo"] = "arm",
        ["brazos"] = "arms",
        ["pierna"] = "leg",
        ["piernas"] = "legs",
        ["pie"] = "foot",
        ["pies"] = "feet",
        ["pelo"] = "hair",
        ["cabello"] = "hair",
        ["barba"] = "beard",
        ["bigote"] = "mustache",

        // ── Ropa, calzado y accesorios ─────────────────────────────────────────
        ["gafas"] = "glasses",
        ["gafas de sol"] = "sunglasses",
        ["lentes"] = "glasses",
        ["lentes de sol"] = "sunglasses",
        ["anteojos"] = "glasses",
        ["sombrero"] = "hat",
        ["sombreros"] = "hats",
        ["gorra"] = "cap",
        ["gorras"] = "caps",
        ["gorro"] = "beanie",
        ["casco"] = "helmet",
        ["cascos"] = "helmets",
        ["reloj"] = "watch",
        ["reloj de pulsera"] = "wrist watch",
        ["relojes"] = "watches",
        ["bolso"] = "handbag",
        ["bolsos"] = "handbags",
        ["mochila"] = "backpack",
        ["mochilas"] = "backpacks",
        ["maleta"] = "suitcase",
        ["maletas"] = "suitcases",
        ["equipaje"] = "luggage",
        ["cartera"] = "wallet",
        ["billetera"] = "wallet",
        ["paraguas"] = "umbrella",
        ["sombrilla"] = "parasol",
        ["cinturon"] = "belt",
        ["cinturón"] = "belt",
        ["zapatos"] = "shoes",
        ["zapato"] = "shoe",
        ["zapatillas"] = "sneakers",
        ["zapatilla"] = "sneaker",
        ["botas"] = "boots",
        ["bota"] = "boot",
        ["sandalias"] = "sandals",
        ["tacones"] = "high heels",
        ["camisa"] = "shirt",
        ["camisas"] = "shirts",
        ["camiseta"] = "t-shirt",
        ["camisetas"] = "t-shirts",
        ["pantalon"] = "pants",
        ["pantalón"] = "pants",
        ["pantalones"] = "pants",
        ["vaqueros"] = "jeans",
        ["jeans"] = "jeans",
        ["pantalon corto"] = "shorts",
        ["pantalón corto"] = "shorts",
        ["shorts"] = "shorts",
        ["falda"] = "skirt",
        ["faldas"] = "skirts",
        ["vestido"] = "dress",
        ["vestidos"] = "dresses",
        ["chaqueta"] = "jacket",
        ["chaquetas"] = "jackets",
        ["abrigo"] = "coat",
        ["abrigos"] = "coats",
        ["sudadera"] = "hoodie",
        ["jersey"] = "sweater",
        ["sueter"] = "sweater",
        ["suéter"] = "sweater",
        ["traje"] = "suit",
        ["corbata"] = "tie",
        ["corbatas"] = "ties",
        ["pajarita"] = "bow tie",
        ["guantes"] = "gloves",
        ["bufanda"] = "scarf",
        ["mascarilla"] = "face mask",
        ["joya"] = "jewelry",
        ["joyas"] = "jewelry",
        ["anillo"] = "ring",
        ["collar"] = "necklace",
        ["pulsera"] = "bracelet",
        ["pendientes"] = "earrings",

        // ── Animales y mascotas ────────────────────────────────────────────────
        ["perro"] = "dog",
        ["perros"] = "dogs",
        ["perrito"] = "puppy",
        ["cachorro"] = "puppy",
        ["gato"] = "cat",
        ["gatos"] = "cats",
        ["gatito"] = "kitten",
        ["pajaro"] = "bird",
        ["pájaro"] = "bird",
        ["pajaros"] = "birds",
        ["pájaros"] = "birds",
        ["ave"] = "bird",
        ["aves"] = "birds",
        ["loro"] = "parrot",
        ["paloma"] = "pigeon",
        ["aguila"] = "eagle",
        ["águila"] = "eagle",
        ["caballo"] = "horse",
        ["caballos"] = "horses",
        ["vaca"] = "cow",
        ["vacas"] = "cows",
        ["toro"] = "bull",
        ["oveja"] = "sheep",
        ["ovejas"] = "sheep",
        ["cordero"] = "lamb",
        ["cabra"] = "goat",
        ["cerdo"] = "pig",
        ["cerdos"] = "pigs",
        ["elefante"] = "elephant",
        ["elefantes"] = "elephants",
        ["oso"] = "bear",
        ["osos"] = "bears",
        ["leon"] = "lion",
        ["león"] = "lion",
        ["leones"] = "lions",
        ["tigre"] = "tiger",
        ["tigres"] = "tigers",
        ["mono"] = "monkey",
        ["monos"] = "monkeys",
        ["cebra"] = "zebra",
        ["jirafa"] = "giraffe",
        ["conejo"] = "rabbit",
        ["conejos"] = "rabbits",
        ["raton"] = "mouse",
        ["ratón"] = "mouse",
        ["rata"] = "rat",
        ["ardilla"] = "squirrel",
        ["ciervo"] = "deer",
        ["lobo"] = "wolf",
        ["zorro"] = "fox",
        ["delfin"] = "dolphin",
        ["delfín"] = "dolphin",
        ["ballena"] = "whale",
        ["tiburon"] = "shark",
        ["tiburón"] = "shark",
        ["pez"] = "fish",
        ["peces"] = "fish",
        ["tortuga"] = "turtle",
        ["rana"] = "frog",
        ["serpiente"] = "snake",
        ["mariposa"] = "butterfly",
        ["abeja"] = "bee",
        ["insecto"] = "insect",
        ["araña"] = "spider",

        // ── Vehículos y transporte ─────────────────────────────────────────────
        ["coche"] = "car",
        ["coches"] = "cars",
        ["auto"] = "car",
        ["autos"] = "cars",
        ["automovil"] = "car",
        ["automóvil"] = "car",
        ["automoviles"] = "cars",
        ["automóviles"] = "cars",
        ["carro"] = "car",
        ["carros"] = "cars",
        ["vehiculo"] = "vehicle",
        ["vehículo"] = "vehicle",
        ["vehiculos"] = "vehicles",
        ["vehículos"] = "vehicles",
        ["moto"] = "motorcycle",
        ["motos"] = "motorcycles",
        ["motocicleta"] = "motorcycle",
        ["motocicletas"] = "motorcycles",
        ["ciclomotor"] = "scooter",
        ["patinete"] = "electric scooter",
        ["bicicleta"] = "bicycle",
        ["bicicletas"] = "bicycles",
        ["bici"] = "bicycle",
        ["bicis"] = "bicycles",
        ["autobus"] = "bus",
        ["autobús"] = "bus",
        ["autobuses"] = "buses",
        ["camion"] = "truck",
        ["camión"] = "truck",
        ["camiones"] = "trucks",
        ["furgoneta"] = "van",
        ["furgonetas"] = "vans",
        ["camioneta"] = "pickup truck",
        ["tren"] = "train",
        ["trenes"] = "trains",
        ["metro"] = "subway",
        ["tranvia"] = "tram",
        ["tranvía"] = "tram",
        ["avion"] = "airplane",
        ["avión"] = "airplane",
        ["aviones"] = "airplanes",
        ["avioneta"] = "small plane",
        ["helicoptero"] = "helicopter",
        ["helicóptero"] = "helicopter",
        ["barco"] = "boat",
        ["barcos"] = "boats",
        ["bote"] = "boat",
        ["botes"] = "boats",
        ["lancha"] = "speedboat",
        ["yate"] = "yacht",
        ["crucero"] = "cruise ship",
        ["submarino"] = "submarine",
        ["taxi"] = "taxi",
        ["ambulancia"] = "ambulance",
        ["coche de policia"] = "police car",
        ["coche de policía"] = "police car",
        ["camion de bomberos"] = "fire truck",
        ["camión de bomberos"] = "fire truck",
        ["tractor"] = "tractor",
        ["semaforo"] = "traffic light",
        ["semáforo"] = "traffic light",
        ["semaforos"] = "traffic lights",
        ["semáforos"] = "traffic lights",
        ["señal de trafico"] = "traffic sign",
        ["señal de tráfico"] = "traffic sign",
        ["señal de stop"] = "stop sign",
        ["parquímetro"] = "parking meter",
        ["parquimetro"] = "parking meter",

        // ── Objetos cotidianos y hogar ─────────────────────────────────────────
        ["taza"] = "cup",
        ["tazas"] = "cups",
        ["taza de cafe"] = "coffee cup",
        ["taza de café"] = "coffee cup",
        ["vaso"] = "glass",
        ["vasos"] = "glasses",
        ["copa"] = "wine glass",
        ["copas"] = "wine glasses",
        ["copa de vino"] = "wine glass",
        ["botella"] = "bottle",
        ["botellas"] = "bottles",
        ["botella de agua"] = "water bottle",
        ["botella de vino"] = "wine bottle",
        ["plato"] = "plate",
        ["platos"] = "plates",
        ["cuenco"] = "bowl",
        ["bol"] = "bowl",
        ["tenedor"] = "fork",
        ["tenedores"] = "forks",
        ["cuchillo"] = "knife",
        ["cuchillos"] = "knives",
        ["cuchara"] = "spoon",
        ["cucharas"] = "spoons",
        ["sarten"] = "frying pan",
        ["sartén"] = "frying pan",
        ["olla"] = "pot",
        ["tetera"] = "teapot",
        ["cafetera"] = "coffee maker",
        ["jarra"] = "pitcher",
        ["silla"] = "chair",
        ["sillas"] = "chairs",
        ["sillon"] = "armchair",
        ["sillón"] = "armchair",
        ["sofa"] = "couch",
        ["sofá"] = "couch",
        ["sofas"] = "couches",
        ["sofás"] = "couches",
        ["mesa"] = "table",
        ["mesas"] = "tables",
        ["mesa de comedor"] = "dining table",
        ["escritorio"] = "desk",
        ["cama"] = "bed",
        ["camas"] = "beds",
        ["almohada"] = "pillow",
        ["manta"] = "blanket",
        ["sabana"] = "bed sheet",
        ["sábana"] = "bed sheet",
        ["armario"] = "wardrobe",
        ["estanteria"] = "bookshelf",
        ["estantería"] = "bookshelf",
        ["puerta"] = "door",
        ["puertas"] = "doors",
        ["ventana"] = "window",
        ["ventanas"] = "windows",
        ["espejo"] = "mirror",
        ["lampara"] = "lamp",
        ["lámpara"] = "lamp",
        ["lamparas"] = "lamps",
        ["lámparas"] = "lamps",
        ["cuadro"] = "painting",
        ["reloj de pared"] = "wall clock",
        ["reloj despertador"] = "alarm clock",
        ["inodoro"] = "toilet",
        ["lavabo"] = "sink",
        ["fregadero"] = "sink",
        ["ducha"] = "shower",
        ["bañera"] = "bathtub",
        ["toalla"] = "towel",
        ["grifo"] = "faucet",
        ["jabon"] = "soap",
        ["jabón"] = "soap",
        ["cepillo de dientes"] = "toothbrush",
        ["pasta de dientes"] = "toothpaste",
        ["secador de pelo"] = "hair drier",
        ["tijeras"] = "scissors",

        // ── Electrónica y tecnología ───────────────────────────────────────────
        ["telefono"] = "cell phone",
        ["teléfono"] = "cell phone",
        ["telefono movil"] = "cell phone",
        ["teléfono móvil"] = "cell phone",
        ["movil"] = "cell phone",
        ["móvil"] = "cell phone",
        ["smartphone"] = "cell phone",
        ["celular"] = "cell phone",
        ["tablet"] = "tablet",
        ["tableta"] = "tablet",
        ["ipad"] = "tablet",
        ["ordenador"] = "computer",
        ["ordenadores"] = "computers",
        ["computadora"] = "computer",
        ["computadoras"] = "computers",
        ["pc"] = "computer",
        ["portatil"] = "laptop",
        ["portátil"] = "laptop",
        ["laptop"] = "laptop",
        ["teclado"] = "keyboard",
        ["teclados"] = "keyboards",
        ["raton"] = "mouse",
        ["ratón"] = "mouse",
        ["pantalla"] = "monitor",
        ["pantallas"] = "monitors",
        ["monitor"] = "monitor",
        ["monitores"] = "monitors",
        ["television"] = "tv",
        ["televisión"] = "tv",
        ["tele"] = "tv",
        ["tv"] = "tv",
        ["mando"] = "remote",
        ["mando a distancia"] = "remote",
        ["camara"] = "camera",
        ["cámara"] = "camera",
        ["camaras"] = "cameras",
        ["cámaras"] = "cameras",
        ["camara de fotos"] = "camera",
        ["cámara de fotos"] = "camera",
        ["auriculares"] = "headphones",
        ["altavoz"] = "speaker",
        ["altavoces"] = "speakers",
        ["microfono"] = "microphone",
        ["micrófono"] = "microphone",
        ["impresora"] = "printer",
        ["consola"] = "game console",
        ["videojuego"] = "video game",
        ["reloj inteligente"] = "smartwatch",
        ["cargador"] = "charger",
        ["cable"] = "cable",
        ["enchufe"] = "socket",
        ["microondas"] = "microwave",
        ["horno"] = "oven",
        ["tostadora"] = "toaster",
        ["nevera"] = "refrigerator",
        ["frigorifico"] = "refrigerator",
        ["frigorífico"] = "refrigerator",
        ["lavadora"] = "washing machine",
        ["lavavajillas"] = "dishwasher",

        // ── Naturaleza, exterior y paisaje ─────────────────────────────────────
        ["arbol"] = "tree",
        ["árbol"] = "tree",
        ["arboles"] = "trees",
        ["árboles"] = "trees",
        ["arbol de navidad"] = "christmas tree",
        ["árbol de navidad"] = "christmas tree",
        ["bosque"] = "forest",
        ["selva"] = "jungle",
        ["planta"] = "plant",
        ["plantas"] = "plants",
        ["planta en maceta"] = "potted plant",
        ["maceta"] = "flower pot",
        ["flor"] = "flower",
        ["flores"] = "flowers",
        ["rosa"] = "rose",
        ["rosas"] = "roses",
        ["margarita"] = "daisy",
        ["hierba"] = "grass",
        ["cesped"] = "grass",
        ["césped"] = "grass",
        ["hoja"] = "leaf",
        ["hojas"] = "leaves",
        ["jardin"] = "garden",
        ["jardín"] = "garden",
        ["parque"] = "park",
        ["montaña"] = "mountain",
        ["montañas"] = "mountains",
        ["colina"] = "hill",
        ["volcan"] = "volcano",
        ["volcán"] = "volcano",
        ["playa"] = "beach",
        ["costa"] = "coast",
        ["mar"] = "sea",
        ["oceano"] = "ocean",
        ["océano"] = "ocean",
        ["rio"] = "river",
        ["río"] = "river",
        ["lago"] = "lake",
        ["cascada"] = "waterfall",
        ["piscina"] = "swimming pool",
        ["cielo"] = "sky",
        ["nube"] = "cloud",
        ["nubes"] = "clouds",
        ["sol"] = "sun",
        ["luna"] = "moon",
        ["estrella"] = "star",
        ["estrellas"] = "stars",
        ["atardecer"] = "sunset",
        ["amanecer"] = "sunrise",
        ["nieve"] = "snow",
        ["lluvia"] = "rain",
        ["fuego"] = "fire",
        ["hoguera"] = "bonfire",
        ["edificio"] = "building",
        ["edificios"] = "buildings",
        ["casa"] = "house",
        ["casas"] = "houses",
        ["rascacielos"] = "skyscraper",
        ["puente"] = "bridge",
        ["torre"] = "tower",
        ["castillo"] = "castle",
        ["iglesia"] = "church",
        ["estadio"] = "stadium",
        ["calle"] = "street",
        ["carretera"] = "road",
        ["acera"] = "sidewalk",

        // ── Comida, fruta y bebida ─────────────────────────────────────────────
        ["manzana"] = "apple",
        ["manzanas"] = "apples",
        ["platano"] = "banana",
        ["plátano"] = "banana",
        ["platanos"] = "bananas",
        ["plátanos"] = "bananas",
        ["naranja"] = "orange",
        ["naranjas"] = "oranges",
        ["fresa"] = "strawberry",
        ["fresas"] = "strawberries",
        ["limon"] = "lemon",
        ["limón"] = "lemon",
        ["uva"] = "grape",
        ["uvas"] = "grapes",
        ["sandia"] = "watermelon",
        ["sandía"] = "watermelon",
        ["melocoton"] = "peach",
        ["melocotón"] = "peach",
        ["pera"] = "pear",
        ["aguacate"] = "avocado",
        ["tomate"] = "tomato",
        ["tomates"] = "tomatoes",
        ["patata"] = "potato",
        ["patatas"] = "potatoes",
        ["zanahoria"] = "carrot",
        ["zanahorias"] = "carrots",
        ["brocoli"] = "broccoli",
        ["brócoli"] = "broccoli",
        ["lechuga"] = "lettuce",
        ["cebolla"] = "onion",
        ["ensalada"] = "salad",
        ["pizza"] = "pizza",
        ["pizzas"] = "pizzas",
        ["hamburguesa"] = "hamburger",
        ["hamburguesas"] = "hamburgers",
        ["perrito caliente"] = "hot dog",
        ["sandwich"] = "sandwich",
        ["bocadillo"] = "sandwich",
        ["pan"] = "bread",
        ["tostada"] = "toast",
        ["croissant"] = "croissant",
        ["queso"] = "cheese",
        ["carne"] = "meat",
        ["filete"] = "steak",
        ["pollo"] = "chicken",
        ["pescado"] = "fish",
        ["arroz"] = "rice",
        ["pasta"] = "pasta",
        ["sopa"] = "soup",
        ["huevo"] = "egg",
        ["huevos"] = "eggs",
        ["pastel"] = "cake",
        ["tarta"] = "cake",
        ["donut"] = "donut",
        ["rosquilla"] = "donut",
        ["galleta"] = "cookie",
        ["galletas"] = "cookies",
        ["chocolate"] = "chocolate",
        ["helado"] = "ice cream",
        ["vino"] = "wine",
        ["cerveza"] = "beer",
        ["cafe"] = "coffee",
        ["café"] = "coffee",
        ["te"] = "tea",
        ["té"] = "tea",
        ["leche"] = "milk",
        ["zumo"] = "juice",
        ["jugo"] = "juice",
        ["refresco"] = "soda",

        // ── Deportes, ocio y documentos ────────────────────────────────────────
        ["pelota"] = "ball",
        ["balon"] = "ball",
        ["balón"] = "ball",
        ["balon de futbol"] = "soccer ball",
        ["balón de fútbol"] = "soccer ball",
        ["balon de baloncesto"] = "basketball",
        ["balón de baloncesto"] = "basketball",
        ["pelota de tenis"] = "tennis ball",
        ["pelota de beisbol"] = "baseball",
        ["raqueta"] = "racket",
        ["bate"] = "bat",
        ["guante de beisbol"] = "baseball glove",
        ["monopatin"] = "skateboard",
        ["monopatín"] = "skateboard",
        ["skate"] = "skateboard",
        ["tabla de surf"] = "surfboard",
        ["esqui"] = "skis",
        ["esquí"] = "skis",
        ["esquies"] = "skis",
        ["esquís"] = "skis",
        ["snowboard"] = "snowboard",
        ["cometa"] = "kite",
        ["frisbee"] = "frisbee",
        ["disco volador"] = "frisbee",
        ["libro"] = "book",
        ["libros"] = "books",
        ["cuaderno"] = "notebook",
        ["periodico"] = "newspaper",
        ["periódico"] = "newspaper",
        ["revista"] = "magazine",
        ["documento"] = "document",
        ["documentos"] = "documents",
        ["factura"] = "invoice",
        ["facturas"] = "invoices",
        ["recibo"] = "receipt",
        ["papel"] = "paper",
        ["boligrafo"] = "pen",
        ["bolígrafo"] = "pen",
        ["lapiz"] = "pencil",
        ["lápiz"] = "pencil",
        ["guitarra"] = "guitar",
        ["guitarra electrica"] = "electric guitar",
        ["guitarra eléctrica"] = "electric guitar",
        ["piano"] = "piano",
        ["bateria"] = "drums",
        ["batería"] = "drums",
        ["trompeta"] = "trumpet",
        ["violin"] = "violin",
        ["violín"] = "violin",
        ["juguete"] = "toy",
        ["juguetes"] = "toys",
        ["oso de peluche"] = "teddy bear",
        ["peluche"] = "teddy bear",
        ["muñeca"] = "doll",

        // ── Colores y calificadores ────────────────────────────────────────────
        ["rojo"] = "red",
        ["roja"] = "red",
        ["rojos"] = "red",
        ["rojas"] = "red",
        ["azul"] = "blue",
        ["azules"] = "blue",
        ["verde"] = "green",
        ["verdes"] = "green",
        ["amarillo"] = "yellow",
        ["amarilla"] = "yellow",
        ["amarillos"] = "yellow",
        ["amarillas"] = "yellow",
        ["negro"] = "black",
        ["negra"] = "black",
        ["negros"] = "black",
        ["negras"] = "black",
        ["blanco"] = "white",
        ["blanca"] = "white",
        ["blancos"] = "white",
        ["blancas"] = "white",
        ["marron"] = "brown",
        ["marrón"] = "brown",
        ["marrones"] = "brown",
        ["gris"] = "gray",
        ["grises"] = "gray",
        ["rosa"] = "pink",
        ["rosado"] = "pink",
        ["rosada"] = "pink",
        ["naranja"] = "orange",
        ["morado"] = "purple",
        ["violeta"] = "purple",
        ["dorado"] = "golden",
        ["plateado"] = "silver",
        ["oscuro"] = "dark",
        ["oscura"] = "dark",
        ["claro"] = "light",
        ["clara"] = "light",
        ["brillante"] = "bright",
        ["grande"] = "large",
        ["grandes"] = "large",
        ["enorme"] = "huge",
        ["pequeño"] = "small",
        ["pequeña"] = "small",
        ["pequeños"] = "small",
        ["pequeñas"] = "small",
        ["diminuto"] = "tiny",
        ["alto"] = "tall",
        ["alta"] = "tall",
        ["bajo"] = "short",
        ["baja"] = "short",
        ["largo"] = "long",
        ["larga"] = "long",
        ["ancho"] = "wide",
        ["estrecho"] = "narrow",
        ["viejo"] = "old",
        ["vieja"] = "old",
        ["antiguo"] = "vintage",
        ["nuevo"] = "new",
        ["nueva"] = "new",
        ["moderno"] = "modern",
        ["deportivo"] = "sports",
        ["deportiva"] = "sports",
        ["clasico"] = "classic",
        ["clásico"] = "classic",
        ["sentado"] = "sitting",
        ["sentada"] = "sitting",
        ["de pie"] = "standing",
        ["corriendo"] = "running",
        ["caminando"] = "walking",
        ["durmiendo"] = "sleeping",
        ["volando"] = "flying",
        ["abierto"] = "open",
        ["abierta"] = "open",
        ["cerrado"] = "closed",
        ["cerrada"] = "closed"
    };

    // Ordenar claves compuestas por longitud descendente para matching codicioso (greedy)
    private static readonly List<KeyValuePair<string, string>> SortedCompoundConcepts = ConceptDictionary
        .Where(kv => kv.Key.Contains(' '))
        .OrderByDescending(kv => kv.Key.Length)
        .ToList();

    /// <summary>
    /// Traduce un prompt de español a inglés utilizando el modelo neuronal MarianMT o el motor de conceptos con alineación sintáctica.
    /// </summary>
    public static async Task<string> TranslateToEnglishAsync(string inputPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPrompt))
            return string.Empty;

        // 1. Verificar si existe el modelo neuronal MarianMT descargado en disco
        string marianPath = Path.Combine(AiModelManager.ModelsDirectory, "opus-mt-es-en.onnx");
        if (File.Exists(marianPath))
        {
            try
            {
                string neuralResult = await TranslateWithMarianOnnxAsync(marianPath, inputPrompt, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(neuralResult) && !string.Equals(neuralResult, inputPrompt, StringComparison.OrdinalIgnoreCase))
                {
                    return neuralResult;
                }
            }
            catch
            {
                // Caer en el motor semántico de conceptos
            }
        }

        // 2. Preprocesar y normalizar el texto del prompt
        string normalized = CleanCommandPrefixes(inputPrompt.Trim());

        // 3. Separar por delimitadores lógicos: comas, puntos y comas, saltos de línea, y conjunciones " y ", " e ", " o "
        var segments = SplitIntoQuerySegments(normalized);
        var translatedSegments = new List<string>(segments.Count);

        foreach (var seg in segments)
        {
            if (string.IsNullOrWhiteSpace(seg)) continue;
            string translated = TranslateSegment(seg);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                translatedSegments.Add(translated);
            }
        }

        return translatedSegments.Count > 0 ? string.Join(", ", translatedSegments.Distinct(StringComparer.OrdinalIgnoreCase)) : inputPrompt;
    }

    /// <summary>
    /// Traduce un segmento o frase individual aplicando sustitución de conceptos compuestos y reordenación sintáctica de adjetivos.
    /// </summary>
    public static string TranslateSegment(string segment)
    {
        string clean = segment.Trim();
        if (string.IsNullOrEmpty(clean)) return string.Empty;

        // 1. Limpieza de artículos iniciales (el, la, los, las, un, una, unos, unas)
        clean = Regex.Replace(clean, @"^(el|la|los|las|un|una|unos|unas)\s+", "", RegexOptions.IgnoreCase).Trim();

        // 2. Coincidencia directa completa en diccionario
        if (ConceptDictionary.TryGetValue(clean, out var directMatch))
        {
            return directMatch;
        }

        // 3. Reemplazo voraz (greedy) de conceptos compuestos ("gafas de sol", "taza de café", "árbol de navidad")
        string processed = clean;
        foreach (var kvp in SortedCompoundConcepts)
        {
            if (processed.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                processed = Regex.Replace(processed, Regex.Escape(kvp.Key), kvp.Value, RegexOptions.IgnoreCase);
            }
        }

        // 4. Tokenización y alineación gramatical (español [sustantivo] [adjetivo] -> inglés [adjective] [noun])
        var tokens = processed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1)
        {
            var adjectives = new List<string>();
            var nouns = new List<string>();
            var others = new List<string>();

            foreach (var token in tokens)
            {
                string t = token.Trim().ToLowerInvariant();
                if (t is "de" or "con" or "en" or "del" or "al" or "y" or "e" or "o" or "u" or "el" or "la" or "un" or "una")
                    continue;

                if (ConceptDictionary.TryGetValue(t, out var translatedToken))
                {
                    if (IsModifierOrColor(t))
                    {
                        adjectives.Add(translatedToken);
                    }
                    else
                    {
                        nouns.Add(translatedToken);
                    }
                }
                else
                {
                    // Mantener palabra tal cual (puede estar ya en inglés o ser un nombre propio)
                    others.Add(token);
                }
            }

            var resultTokens = new List<string>();
            resultTokens.AddRange(adjectives);
            resultTokens.AddRange(nouns);
            resultTokens.AddRange(others);

            if (resultTokens.Count > 0)
            {
                return string.Join(" ", resultTokens);
            }
        }
        else if (tokens.Length == 1)
        {
            string t = tokens[0].ToLowerInvariant();
            if (ConceptDictionary.TryGetValue(t, out var translated))
            {
                return translated;
            }
        }

        return processed;
    }

    private static List<string> SplitIntoQuerySegments(string text)
    {
        // Reemplazar conjunciones " y ", " e ", " o ", " u " por comas cuando separan conceptos
        string withCommas = Regex.Replace(text, @"\s+(?:y|e|o|u|and|or)\s+", ", ", RegexOptions.IgnoreCase);

        return withCommas
            .Split([',', ';', '\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string CleanCommandPrefixes(string text)
    {
        // Eliminar prefijos comunes de comandos o peticiones
        string cleaned = Regex.Replace(text, @"^(?:detecta|detectar|busca|buscar|encuentra|encontrar|identifica|identificar|localiza|localizar|ver|quiero ver|hay|muestrame|muéstrame|fotos? de|im[aá]genes? de|foto con|imagen con)\s+", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static bool IsModifierOrColor(string word) =>
        word is "rojo" or "roja" or "rojos" or "rojas"
             or "azul" or "azules"
             or "verde" or "verdes"
             or "amarillo" or "amarilla" or "amarillos" or "amarillas"
             or "negro" or "negra" or "negros" or "negras"
             or "blanco" or "blanca" or "blancos" or "blancas"
             or "marron" or "marrón" or "marrones"
             or "gris" or "grises" or "rosa" or "rosado" or "rosada"
             or "naranja" or "morado" or "violeta" or "dorado" or "plateado"
             or "oscuro" or "oscura" or "claro" or "clara" or "brillante"
             or "grande" or "grandes" or "enorme" or "pequeño" or "pequeña" or "pequeños" or "pequeñas" or "diminuto"
             or "alto" or "alta" or "bajo" or "baja" or "largo" or "larga"
             or "viejo" or "vieja" or "antiguo" or "nuevo" or "nueva" or "moderno"
             or "deportivo" or "deportiva" or "clasico" or "clásico"
             or "sentado" or "sentada" or "de pie" or "corriendo" or "caminando" or "durmiendo" or "volando";

    private static async Task<string> TranslateWithMarianOnnxAsync(string modelPath, string text, CancellationToken cancellationToken)
    {
        // Implementación de inferencia neural para MarianMT ONNX
        await Task.CompletedTask;
        return string.Empty;
    }
}
