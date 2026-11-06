// Slika sa telefona zna da ima 5-10 MB ili ime bez ekstenzije (Android galerija), pa je server odbije.
// Ovde se smanjuje na najviše maxSize piksela i šalje kao JPEG; ako pregledač ne ume da je pročita (npr. HEIC),
// vraća se original, a server javlja grešku.
export async function shrinkImage(file: File, maxSize = 1600, quality = 0.85): Promise<File> {
  try {
    const bitmap = await createImageBitmap(file);
    const ratio = Math.min(1, maxSize / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * ratio);
    canvas.height = Math.round(bitmap.height * ratio);
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
    if (!blob) return file;
    const name = (file.name.replace(/\.[^.]*$/, '') || 'slika') + '.jpg';
    return new File([blob], name, { type: 'image/jpeg' });
  } catch {
    return file;
  }
}
