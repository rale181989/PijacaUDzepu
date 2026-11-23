// Verzija aplikacije, prikazana u "Moj nalog" (da se zna šta je ko instalirao kad prijavi problem).
// Jedino mesto za verziju je "version" u package.json: odatle je čitaju i ovaj fajl i Android build
// (android/app/build.gradle: versionName = verzija, versionCode = major*10000 + minor*100 + patch).
import packageJson from '../../package.json';

export const APP_VERSION: string = packageJson.version;
