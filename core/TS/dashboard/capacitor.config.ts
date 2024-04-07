import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'io.ionic.starter',
  appName: 'dashboard',
  webDir: '../../dist/core/dashboard',
  bundledWebRuntime: false,
  server: {
    androidScheme: 'https',
  },
};

export default config;
