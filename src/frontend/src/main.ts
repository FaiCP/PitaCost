// src/main.ts
// Punto de entrada de la aplicacion Angular.
// bootstrapApplication usa el modelo standalone (sin NgModule raiz).

import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig).catch(console.error);
