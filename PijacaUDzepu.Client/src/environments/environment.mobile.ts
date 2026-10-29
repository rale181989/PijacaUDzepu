import { DEFAULT_API_SERVER, getApiServer } from '../app/services/api-server';

export const environment = {
  production: true,
  // Mobilna aplikacija nije na istom domenu kao API, pa joj treba puna adresa.
  // Podrazumevano Render; na ekranu za prijavu može se izabrati i lokalni server.
  apiUrl: getApiServer() + '/api',
  // Javna adresa sajta za linkove koji se dele (npr. link za praćenje porudžbine)
  publicUrl: DEFAULT_API_SERVER,
  serverSelectable: true,
  // Uključiti tek kada se android/app/google-services.json doda u projekat:
  // bez njega PushNotifications.register() ruši Android aplikaciju u nativnom kodu
  pushEnabled: false
};
