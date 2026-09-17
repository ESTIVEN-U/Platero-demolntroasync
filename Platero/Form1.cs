using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Platero
{
    public partial class Form1 : Form
    {
        private readonly HttpClient httpClient = new HttpClient(); // antes estaba local en el constructor
        private static readonly Dictionary<string, byte[]> cacheDescargas = new Dictionary<string, byte[]>();
        private static readonly SemaphoreSlim cacheLock = new SemaphoreSlim(1, 1);

        public Form1()
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Platero/1.0"); // Wikimedia bloquea con 403 si no hay User-Agent
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            pictureBox1.Visible = true;

            var directorioActual = AppDomain.CurrentDomain.BaseDirectory;
            var destinoBaseSecuencial = Path.Combine(directorioActual, @"Imagenes\resultado-secuencial");
            var destinoBaseParalelo = Path.Combine(directorioActual, @"Imagenes\resultado-paralelo");
            PrepararEjecución(destinoBaseParalelo, destinoBaseSecuencial);

            Console.WriteLine("inicio");
            List<Imagen> imagenes = ObtenerImagenes();

            var sw = new Stopwatch();
            sw.Start();

            foreach (var imagen in imagenes)
            {
                await ProcesarImagen(destinoBaseSecuencial, imagen);
            }

            sw.Stop();

            var duración = $"El programa se ejecutó en {sw.ElapsedMilliseconds / 1000.0} segundos";
            Console.WriteLine(duración);

            sw.Reset();
            sw.Start();

            var tareasEnumerable = imagenes.Select(async imagen =>
            {
                await ProcesarImagen(destinoBaseParalelo, imagen);
            });

            await Task.WhenAll(tareasEnumerable);

            Console.WriteLine("Paralelo - duración en segundos: {0}",
                sw.ElapsedMilliseconds / 1000.0);

            sw.Stop();

            pictureBox1.Visible = false;

            // NOTA: no vi en las imágenes en qué punto exacto del click se llaman
            // estos métodos, así que los agrego aquí al final como propuesta —
            // ajustá el orden si en el video estaban en otro lugar.
            await Task.WhenAll(
                RealizarProcesamientoLargoA(),
                RealizarProcesamientoLargoB(),
                RealizarProcesamientoLargoC());

            string resultado = await ProcesamientoLargo();
            Console.WriteLine(resultado);
        }

        private void PrepararEjecución(string destinoParalelo, string destinoSecuencial)
        {
            Directory.CreateDirectory(destinoParalelo);
            Directory.CreateDirectory(destinoSecuencial);
            BorrarArchivos(destinoParalelo);
            BorrarArchivos(destinoSecuencial);
        }

        private static List<Imagen> ObtenerImagenes()
        {
            var imagenes = new List<Imagen>();

            for (int i = 0; i < 7; i++)
            {
                imagenes.Add(
                    new Imagen()
                    {
                        Nombre = $"Cacicazgos {i}.png",
                        Url = "https://upload.wikimedia.org/wikipedia/commons/8/8d/Copia_de_Cacicazgos_de_la_Hispaniola.png?utm_source=es.wikipedia.org&utm_campaign=index&utm_content=original"
                    });
                imagenes.Add(
                    new Imagen()
                    {
                        Nombre = $"Desangles {i}.jpg",
                        Url = "https://upload.wikimedia.org/wikipedia/commons/4/43/Desangles_Colon_engrillado.jpg"
                    });
                imagenes.Add(
                    new Imagen()
                    {
                        Nombre = $"Alcazar {i}.jpg",
                        Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d7/Santo_Domingo_-_Alc%C3%A1zar_de_Col%C3%B3n_0777.JPG/1920px-Santo_Domingo_-_Alc%C3%A1zar_de_Col%C3%B3n_0777.JPG?utm_source=es.wikipedia.org&utm_campaign=index&utm_content=thumbnail"
                    });
            }

            return imagenes;
        }

        private async Task<byte[]> DescargarConCache(string url)
        {
            // Las 3 URLs se piden 7 veces cada una en el ciclo; sin caché eso dispara
            // 21 (o 42 si sumás secuencial + paralelo) pedidos casi simultáneos a
            // Wikimedia, que responde 429 (Too Many Requests). Con caché, solo se
            // descarga una vez por URL y el resto de las copias reusan los bytes.
            await cacheLock.WaitAsync();
            try
            {
                if (cacheDescargas.TryGetValue(url, out var bytesCacheados))
                {
                    return bytesCacheados;
                }

                var respuesta = await httpClient.GetAsync(url);
                respuesta.EnsureSuccessStatusCode();
                var bytes = await respuesta.Content.ReadAsByteArrayAsync();
                cacheDescargas[url] = bytes;
                return bytes;
            }
            finally
            {
                cacheLock.Release();
            }
        }

        private async Task ProcesarImagen(string directorio, Imagen imagen)
        {
            var contenido = await DescargarConCache(imagen.Url);

            Bitmap bitmap;
            using (var ms = new MemoryStream(contenido))
            {
                bitmap = new Bitmap(ms);
            }

            bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
            var destino = Path.Combine(directorio, imagen.Nombre);
            bitmap.Save(destino);
        }

        private void BorrarArchivos(string directorio)
        {
            var archivos = Directory.EnumerateFiles(directorio);
            foreach (var archivo in archivos)
            {
                File.Delete(archivo);
            }
        }

        private async Task<string> ProcesamientoLargo()
        {
            await Task.Delay(3000); // asíncrono
            return "Felipe";
        }

        private async Task RealizarProcesamientoLargoA()
        {
            await Task.Delay(1000); // Asíncrona
            Console.WriteLine("Proceso A finalizado");
        }

        private async Task RealizarProcesamientoLargoB()
        {
            await Task.Delay(1000); // Asíncrona
            Console.WriteLine("Proceso B finalizado");
        }

        private async Task RealizarProcesamientoLargoC()
        {
            await Task.Delay(1000); // Asíncrona
            Console.WriteLine("Proceso C finalizado");
        }
    }
}