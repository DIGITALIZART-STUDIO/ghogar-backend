using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using GestionHogar.Configuration;
using Microsoft.Extensions.Options;

namespace GestionHogar.Services;

public interface ICloudflareService
{
    Task<string> UploadImageAsync(IFormFile file);
    Task<string> UpdateImageAsync(IFormFile file, string existingFileName);
    Task<string> UploadProjectImageAsync(IFormFile file, string projectName);
    Task<string> UpdateProjectImageAsync(
        IFormFile file,
        string projectName,
        string? existingImageUrl
    );
    Task<string> UploadPaymentReceiptImageAsync(IFormFile file, string transactionId);
    Task<string> UpdatePaymentReceiptImageAsync(
        IFormFile file,
        string transactionId,
        string? existingImageUrl
    );
    Task<bool> DeletePaymentTransactionFolderAsync(string transactionId);
    Task<(bool Success, string Message, string? ErrorDetails)> TestConnectionAsync();
}

public class CloudflareService : ICloudflareService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrl;

    public CloudflareService(IOptions<CloudflareR2Configuration> configuration)
    {
        var config = configuration.Value;

        // Config global para S3 compatible con Cloudflare R2
        Amazon.AWSConfigsS3.UseSignatureVersion4 = true; // Usar firma V4

        // Normalizar valores (trim) para evitar espacios ocultos
        var accessKeyId = (config.AccessKeyId ?? string.Empty).Trim();
        var secretAccessKey = (config.SecretAccessKey ?? string.Empty).Trim();
        var serviceUrl = (config.ApiS3 ?? string.Empty).Trim();
        _bucketName = (config.BucketName ?? string.Empty).Trim();
        _publicUrl = (config.PublicUrlImage ?? string.Empty).Trim();

        // Validar configuración
        if (string.IsNullOrEmpty(accessKeyId))
            throw new ArgumentException("AccessKeyId no puede estar vacío");

        if (string.IsNullOrEmpty(secretAccessKey))
            throw new ArgumentException("SecretAccessKey no puede estar vacío");

        if (string.IsNullOrEmpty(serviceUrl))
            throw new ArgumentException("ApiS3 no puede estar vacío");

        // Configurar el cliente S3 para Cloudflare R2
        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl, // Endpoint de Cloudflare R2
            ForcePathStyle = true, // Necesario para Cloudflare R2
            UseHttp = false, // Usar HTTPS
            DisableHostPrefixInjection = true, // Deshabilitar la inyección de prefijo de host
            // Configuraciones adicionales para R2
            UseAccelerateEndpoint = false, // Deshabilitar aceleración
            UseDualstackEndpoint = false, // Deshabilitar dual stack
        };

        // Crear el cliente S3 con configuración específica para Cloudflare R2
        _s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, s3Config);
    }

    /// <summary>
    /// Subir una imagen a Cloudflare R2
    /// </summary>
    /// <param name="file">Archivo a subir</param>
    /// <returns>URL pública del archivo subido</returns>
    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo no puede estar vacío");

        // Validar el tipo de archivo
        if (!IsValidImageFile(file))
            throw new ArgumentException("El archivo debe ser una imagen válida (JPG, PNG, WEBP)");

        // Obtener la extensión del archivo
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{fileExtension}";

        // Leer el archivo completo en memoria para evitar streaming
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        // Crear la solicitud de subida usando bytes en lugar de stream
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            InputStream = new MemoryStream(fileBytes),
            ContentType = GetContentType(fileExtension),
            CannedACL = S3CannedACL.PublicRead, // Hacer el archivo público
        };

        try
        {
            // Subir el archivo
            await _s3Client.PutObjectAsync(putRequest);

            // Retornar la URL pública
            return $"{_publicUrl}/{fileName}";
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al subir archivo a Cloudflare R2: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Subir una imagen de proyecto a Cloudflare R2 en la carpeta projects
    /// </summary>
    /// <param name="file">Archivo a subir</param>
    /// <param name="projectName">Nombre del proyecto para crear la carpeta</param>
    /// <returns>URL pública del archivo subido</returns>
    public async Task<string> UploadProjectImageAsync(IFormFile file, string projectName)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo no puede estar vacío");

        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("El nombre del proyecto no puede estar vacío");

        // Validar el tipo de archivo
        if (!IsValidImageFile(file))
            throw new ArgumentException("El archivo debe ser una imagen válida (JPG, PNG, WEBP)");

        // Obtener la extensión del archivo
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{fileExtension}";

        // Crear la ruta en la carpeta projects/nombre-del-proyecto
        var sanitizedProjectName = SanitizeProjectName(projectName);
        var key = $"projects/{sanitizedProjectName}/{fileName}";

        // Leer el archivo completo en memoria como bytes
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        try
        {
            using var inputStream = new MemoryStream(fileBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = inputStream,
                ContentType = GetContentType(fileExtension),
                CannedACL = S3CannedACL.PublicRead,
                DisablePayloadSigning = true, // clave para R2: evita streaming-signed payload
            };
            // Forzar Content-Length para evitar chunked
            putRequest.Headers.ContentLength = fileBytes.Length;

            await _s3Client.PutObjectAsync(putRequest);

            // Retornar la URL pública
            return $"{_publicUrl}/{key}";
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al subir archivo de proyecto a Cloudflare R2: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Actualizar una imagen en Cloudflare R2
    /// </summary>
    /// <param name="file">Archivo a actualizar</param>
    /// <param name="existingFileName">Nombre del archivo existente (solo el nombre, sin URL)</param>
    /// <returns>URL pública del archivo actualizado</returns>
    public async Task<string> UpdateImageAsync(IFormFile file, string existingFileName)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo no puede estar vacío");

        // Extraer la extensión del nuevo archivo
        var newExtension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{newExtension}";

        // Eliminar el archivo existente si existe
        if (!string.IsNullOrEmpty(existingFileName))
        {
            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = existingFileName,
                };
                await _s3Client.DeleteObjectAsync(deleteRequest);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                // El archivo no existe, continuar
            }
        }

        // Leer el archivo completo en memoria para evitar streaming
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        // Crear la solicitud de subida del nuevo archivo usando bytes
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            InputStream = new MemoryStream(fileBytes),
            ContentType = GetContentType(Path.GetExtension(file.FileName).ToLowerInvariant()),
            CannedACL = S3CannedACL.PublicRead, // Hacer el archivo público
        };

        try
        {
            // Subir el nuevo archivo
            await _s3Client.PutObjectAsync(putRequest);
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al actualizar archivo en Cloudflare R2: {ex.Message}"
            );
        }

        // Retornar la URL pública del archivo actualizado
        return $"{_publicUrl}/{fileName}";
    }

    /// <summary>
    /// Actualizar una imagen de proyecto en Cloudflare R2
    /// </summary>
    /// <param name="file">Archivo a actualizar</param>
    /// <param name="projectName">Nombre del proyecto</param>
    /// <param name="existingImageUrl">URL de la imagen existente</param>
    /// <returns>URL pública del archivo actualizado</returns>
    public async Task<string> UpdateProjectImageAsync(
        IFormFile file,
        string projectName,
        string? existingImageUrl
    )
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo no puede estar vacío");

        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("El nombre del proyecto no puede estar vacío");

        // Extraer la extensión del nuevo archivo
        var newExtension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{newExtension}";

        // Crear la ruta en la carpeta projects/nombre-del-proyecto
        var sanitizedProjectName = SanitizeProjectName(projectName);
        var key = $"projects/{sanitizedProjectName}/{fileName}";

        // Eliminar el archivo existente si existe
        if (!string.IsNullOrEmpty(existingImageUrl))
        {
            try
            {
                // Extraer la clave del archivo existente de la URL
                var existingKey = ExtractKeyFromUrl(existingImageUrl);
                if (!string.IsNullOrEmpty(existingKey))
                {
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = existingKey,
                    };
                    await _s3Client.DeleteObjectAsync(deleteRequest);
                }
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                // El archivo no existe, continuar
            }
        }

        // Leer el archivo completo en memoria como bytes
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        try
        {
            using var inputStream = new MemoryStream(fileBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = inputStream,
                ContentType = GetContentType(Path.GetExtension(file.FileName).ToLowerInvariant()),
                CannedACL = S3CannedACL.PublicRead,
                DisablePayloadSigning = true, // clave para R2: evita streaming-signed payload
            };
            // Forzar Content-Length para evitar chunked
            putRequest.Headers.ContentLength = fileBytes.Length;

            await _s3Client.PutObjectAsync(putRequest);

            // Retornar la URL pública del archivo actualizado
            return $"{_publicUrl}/{key}";
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al actualizar archivo de proyecto en Cloudflare R2: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Subir una imagen de recibo de pago a Cloudflare R2
    /// </summary>
    /// <param name="file">Archivo del recibo de pago</param>
    /// <param name="transactionId">ID de la transacción</param>
    /// <returns>URL pública del archivo subido</returns>
    public async Task<string> UploadPaymentReceiptImageAsync(IFormFile file, string transactionId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo del recibo de pago no puede estar vacío");

        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("El ID de la transacción no puede estar vacío");

        // Validar el tipo de archivo
        if (!IsValidImageFile(file))
            throw new ArgumentException(
                "El archivo del recibo de pago debe ser una imagen válida (JPG, PNG, WEBP)"
            );

        // Obtener la extensión del archivo
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{fileExtension}";

        // Crear la ruta en la carpeta payments/id-de-transaccion
        var key = $"payments/{transactionId}/{fileName}";

        // Leer el archivo completo en memoria como bytes
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        try
        {
            using var inputStream = new MemoryStream(fileBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = inputStream,
                ContentType = GetContentType(fileExtension),
                CannedACL = S3CannedACL.PublicRead,
                DisablePayloadSigning = true, // clave para R2: evita streaming-signed payload
            };
            // Forzar Content-Length para evitar chunked
            putRequest.Headers.ContentLength = fileBytes.Length;

            await _s3Client.PutObjectAsync(putRequest);

            // Retornar la URL pública
            return $"{_publicUrl}/{key}";
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al subir recibo de pago a Cloudflare R2: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Actualizar una imagen de recibo de pago en Cloudflare R2
    /// </summary>
    /// <param name="file">Archivo del recibo de pago a actualizar</param>
    /// <param name="transactionId">ID de la transacción</param>
    /// <param name="existingImageUrl">URL de la imagen existente</param>
    /// <returns>URL pública del archivo actualizado</returns>
    public async Task<string> UpdatePaymentReceiptImageAsync(
        IFormFile file,
        string transactionId,
        string? existingImageUrl
    )
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("El archivo del recibo de pago no puede estar vacío");

        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("El ID de la transacción no puede estar vacío");

        // Extraer la extensión del nuevo archivo
        var newExtension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{newExtension}";

        // Crear la ruta en la carpeta payments/id-de-transaccion
        var key = $"payments/{transactionId}/{fileName}";

        // Eliminar el archivo existente si existe
        if (!string.IsNullOrEmpty(existingImageUrl))
        {
            try
            {
                // Extraer la clave del archivo existente de la URL
                var existingKey = ExtractKeyFromUrl(existingImageUrl);
                if (!string.IsNullOrEmpty(existingKey))
                {
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = existingKey,
                    };
                    await _s3Client.DeleteObjectAsync(deleteRequest);
                }
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                // El archivo no existe, continuar
            }
        }

        // Leer el archivo completo en memoria como bytes
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        try
        {
            using var inputStream = new MemoryStream(fileBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = inputStream,
                ContentType = GetContentType(Path.GetExtension(file.FileName).ToLowerInvariant()),
                CannedACL = S3CannedACL.PublicRead,
                DisablePayloadSigning = true, // clave para R2: evita streaming-signed payload
            };
            // Forzar Content-Length para evitar chunked
            putRequest.Headers.ContentLength = fileBytes.Length;

            await _s3Client.PutObjectAsync(putRequest);

            // Retornar la URL pública del archivo actualizado
            return $"{_publicUrl}/{key}";
        }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al actualizar recibo de pago en Cloudflare R2: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Elimina todos los archivos dentro de una carpeta de transacción de pagos
    /// </summary>
    /// <param name="transactionId">ID de la transacción</param>
    /// <returns>True si la carpeta se eliminó, false si no existe o hubo un error</returns>
    public async Task<bool> DeletePaymentTransactionFolderAsync(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new ArgumentException("El ID de la transacción no puede estar vacío");
        }

        var prefix = $"payments/{transactionId}/";

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                MaxKeys = 1000, // Obtener todos los objetos en la carpeta
            };

            var response = await _s3Client.ListObjectsV2Async(listRequest);

            var objectsToDelete = response
                .S3Objects.Select(obj => new KeyVersion { Key = obj.Key })
                .ToList();

            if (objectsToDelete.Count > 0)
            {
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = objectsToDelete,
                };
                await _s3Client.DeleteObjectsAsync(deleteRequest);
                return true;
            }
            return false; // No hay objetos para eliminar
        }
        catch (AmazonS3Exception ex)
        {
            if (ex.ErrorCode == "NoSuchBucket")
            {
                return false; // El bucket no existe
            }
            throw new InvalidOperationException(
                $"Error al eliminar archivos de la carpeta de transacción {transactionId}: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Error inesperado al eliminar archivos de la carpeta de transacción {transactionId}: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Sanitiza el nombre del proyecto para usarlo como nombre de carpeta
    /// </summary>
    /// <param name="projectName">Nombre del proyecto</param>
    /// <returns>Nombre sanitizado</returns>
    private static string SanitizeProjectName(string projectName)
    {
        return projectName
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Replace(",", "-")
            .Replace(";", "-")
            .Replace(":", "-")
            .Replace("!", "-")
            .Replace("?", "-")
            .Replace("(", "-")
            .Replace(")", "-")
            .Replace("[", "-")
            .Replace("]", "-")
            .Replace("{", "-")
            .Replace("}", "-")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("|", "-")
            .Replace("&", "-")
            .Replace("+", "-")
            .Replace("=", "-")
            .Replace("@", "-")
            .Replace("#", "-")
            .Replace("$", "-")
            .Replace("%", "-")
            .Replace("^", "-")
            .Replace("*", "-")
            .Replace("~", "-")
            .Replace("`", "-")
            .Replace("'", "-")
            .Replace("\"", "-")
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ú", "u")
            .Replace("ñ", "n")
            .Replace("ü", "u")
            .Replace("Á", "a")
            .Replace("É", "e")
            .Replace("Í", "i")
            .Replace("Ó", "o")
            .Replace("Ú", "u")
            .Replace("Ñ", "n")
            .Replace("Ü", "u");
    }

    /// <summary>
    /// Extrae la clave del archivo de una URL de Cloudflare R2
    /// </summary>
    /// <param name="url">URL del archivo</param>
    /// <returns>Clave del archivo o null si no se puede extraer</returns>
    private string? ExtractKeyFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;

            // Remover el slash inicial si existe
            if (path.StartsWith("/"))
                path = path.Substring(1);

            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Valida si el archivo es una imagen válida
    /// </summary>
    /// <param name="file">Archivo a validar</param>
    /// <returns>True si es una imagen válida</returns>
    private static bool IsValidImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return false;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var allowedContentTypes = new[]
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/webp",
            "image/gif",
        };

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = file.ContentType.ToLowerInvariant();

        return allowedExtensions.Contains(extension) && allowedContentTypes.Contains(contentType);
    }

    /// <summary>
    /// Obtiene el content type basado en la extensión del archivo
    /// </summary>
    /// <param name="extension">Extensión del archivo</param>
    /// <returns>Content type</returns>
    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Prueba la conexión con Cloudflare R2 y retorna detalles del error
    /// </summary>
    /// <returns>Objeto con el resultado de la prueba y detalles del error</returns>
    public async Task<(bool Success, string Message, string? ErrorDetails)> TestConnectionAsync()
    {
        try
        {
            // Verificar configuración básica
            if (string.IsNullOrEmpty(_bucketName))
            {
                return (false, "Error de configuración: BucketName está vacío", null);
            }

            if (string.IsNullOrEmpty(_publicUrl))
            {
                return (false, "Error de configuración: PublicUrlImage está vacío", null);
            }

            // Intentar listar objetos del bucket para verificar la conexión
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                MaxKeys = 1, // Solo necesitamos verificar que podemos conectarnos
            };

            await _s3Client.ListObjectsV2Async(listRequest);
            return (true, "Conexión exitosa con Cloudflare R2", null);
        }
        catch (AmazonS3Exception ex)
        {
            var errorMessage = ex.ErrorCode switch
            {
                "NoSuchBucket" => $"El bucket '{_bucketName}' no existe",
                "AccessDenied" => "Acceso denegado. Verifica las credenciales y permisos",
                "InvalidAccessKeyId" => "AccessKeyId inválido o no existe",
                "SignatureDoesNotMatch" => "Error en la firma. Verifica la SecretAccessKey",
                "InvalidToken" => "Token de autenticación inválido",
                "ExpiredToken" => "Token de autenticación expirado",
                "InvalidSecurity" => "Error de seguridad en la autenticación",
                _ => $"Error de AWS S3: {ex.ErrorCode} - {ex.Message}",
            };

            return (false, errorMessage, ex.ToString());
        }
        catch (Exception ex)
        {
            return (false, $"Error inesperado: {ex.Message}", ex.ToString());
        }
    }
}
