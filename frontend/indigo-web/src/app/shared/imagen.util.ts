export const EXTENSIONES_IMAGEN = ['jpg', 'jpeg', 'png', 'webp'];

export const TAMANO_MAXIMO_IMAGEN = 5 * 1024 * 1024;

export function validarImagen(archivo: File): string | null {
  const extension = archivo.name.split('.').pop()?.toLowerCase() ?? '';
  if (!EXTENSIONES_IMAGEN.includes(extension)) {
    return 'Formato no permitido. Usá JPG, PNG o WebP.';
  }
  if (archivo.size > TAMANO_MAXIMO_IMAGEN) {
    return 'La imagen supera los 5 MB permitidos.';
  }
  return null;
}
